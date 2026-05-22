using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Web;

namespace LanTransfer.Services;

internal static class DebugLog
{
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "lantransfer_debug.log");
    public static void Write(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n"); } catch { }
    }
}

public class HttpServerService : IDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly string _connectionCode;
    private readonly string _saveDirectory;

    public event Action<TransferRecord>? OnTransferCompleted;
    public event Action<string, long>? OnFileReceived;
    public event Action<string>? OnLog;
    public event Action<List<ConnectedDevice>>? OnDevicesChanged;

    private readonly ConcurrentDictionary<string, (string FilePath, string FileName, long FileSize)> _sharedFiles = new();
    private readonly ConcurrentDictionary<string, StreamWriter> _sseClients = new();
    private readonly ConcurrentDictionary<string, ConnectedDevice> _connectedDevices = new();
    private readonly SemaphoreSlim _concurrencyLimiter = new(4, 4); // 最多 4 个并发传输

    public HttpServerService(string connectionCode)
    {
        _connectionCode = connectionCode;
        _saveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "LanTransfer", "Received");
        Directory.CreateDirectory(_saveDirectory);
    }

    public async Task StartAsync(string ip, int port)
    {
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        OnLog?.Invoke($"服务器已启动: http://{ip}:{port}/");

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleConnectionAsync(client));
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
    }

    public string AddSharedFile(string filePath)
    {
        var fileId = Guid.NewGuid().ToString("N")[..8];
        var info = new FileInfo(filePath);
        _sharedFiles[fileId] = (filePath, info.Name, info.Length);
        _ = BroadcastFileListAsync();
        return fileId;
    }

    public bool RemoveSharedFile(string fileId)
    {
        var result = _sharedFiles.TryRemove(fileId, out _);
        if (result) _ = BroadcastFileListAsync();
        return result;
    }

    private async Task BroadcastFileListAsync()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(GetSharedFiles().Select(f => new { id = f.FileId, name = f.FileName, size = FormatSize(f.FileSize) }));
        var data = $"data: {json}\n\n";

        foreach (var kvp in _sseClients)
        {
            try
            {
                await kvp.Value.WriteAsync(data);
                await kvp.Value.FlushAsync();
            }
            catch
            {
                _sseClients.TryRemove(kvp.Key, out _);
            }
        }
    }

    public List<SharedFileInfo> GetSharedFiles()
    {
        return _sharedFiles.Select(f => new SharedFileInfo
        {
            FileId = f.Key,
            FileName = f.Value.FileName,
            FileSize = f.Value.FileSize
        }).ToList();
    }

    public List<ConnectedDevice> GetConnectedDevices()
    {
        return _connectedDevices.Values.OrderByDescending(d => d.LastSeen).ToList();
    }

    private void TrackDevice(string ip, string? userAgent)
    {
        var name = ParseDeviceName(userAgent);
        var device = new ConnectedDevice { Ip = ip, Name = name, LastSeen = DateTime.Now };
        _connectedDevices[ip] = device;

        // 清理超过 5 分钟未活动的设备
        var cutoff = DateTime.Now.AddMinutes(-5);
        foreach (var kvp in _connectedDevices)
        {
            if (kvp.Value.LastSeen < cutoff)
                _connectedDevices.TryRemove(kvp.Key, out _);
        }

        OnDevicesChanged?.Invoke(GetConnectedDevices());
    }

    private static string ParseDeviceName(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent)) return "未知设备";

        if (userAgent.Contains("iPhone")) return "iPhone";
        if (userAgent.Contains("iPad")) return "iPad";
        if (userAgent.Contains("Android") && userAgent.Contains("Mobile")) return "Android 手机";
        if (userAgent.Contains("Android")) return "Android 平板";
        if (userAgent.Contains("Macintosh") || userAgent.Contains("Mac OS")) return "Mac";
        if (userAgent.Contains("Windows NT 10")) return "Windows PC";
        if (userAgent.Contains("Windows NT 6.3")) return "Windows 8.1";
        if (userAgent.Contains("Windows NT 6.1")) return "Windows 7";
        if (userAgent.Contains("Windows")) return "Windows PC";
        if (userAgent.Contains("Linux")) return "Linux PC";

        return "未知设备";
    }

    private async Task HandleConnectionAsync(TcpClient client)
    {
        using (client)
        {
            // 设置 linger：关闭时等待数据发完再发 FIN，避免 Safari 收到 RST
            client.Client.LingerState = new LingerOption(true, 60);

            var remoteIp = ((IPEndPoint?)client.Client.RemoteEndPoint)?.Address.ToString() ?? "unknown";
            using var stream = client.GetStream();

            var (headerBytes, leftover, leftoverCount) = await ReadHeadersAsync(stream);
            if (headerBytes.Count == 0) return;

            DebugLog.Write($"Headers read: {headerBytes.Count} bytes, leftover: {leftoverCount} bytes");

            using var bodyStream = new PrefixedStream(stream, leftover, leftoverCount);

            var headerText = Encoding.UTF8.GetString(headerBytes.ToArray());
            DebugLog.Write($"Request: {headerText[..Math.Min(200, headerText.Length)].Replace("\r\n", " | ")}");
            var lines = headerText.Split("\r\n", StringSplitOptions.None);
            if (lines.Length == 0) return;

            var requestParts = lines[0].Split(' ', 3);
            if (requestParts.Length < 2) return;

            var method = requestParts[0];
            var rawPath = requestParts[1];

            var queryIdx = rawPath.IndexOf('?');
            var path = queryIdx >= 0 ? rawPath[..queryIdx] : rawPath;
            var query = queryIdx >= 0 ? rawPath[(queryIdx + 1)..] : "";

            var queryDict = ParseQueryString(query);
            queryDict.TryGetValue("code", out var code);

            long contentLength = 0;
            string? contentType = null;
            string? rangeHeader = null;
            string? userAgent = null;
            bool expectContinue = false;
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                var colonIdx = line.IndexOf(':');
                if (colonIdx > 0)
                {
                    var key = line[..colonIdx].Trim();
                    var value = line[(colonIdx + 1)..].Trim();
                    if (key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                        long.TryParse(value, out contentLength);
                    else if (key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                        contentType = value;
                    else if (key.Equals("Range", StringComparison.OrdinalIgnoreCase))
                        rangeHeader = value;
                    else if (key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))
                        userAgent = value;
                    else if (key.Equals("Expect", StringComparison.OrdinalIgnoreCase) && value.Contains("100-continue", StringComparison.OrdinalIgnoreCase))
                        expectContinue = true;
                }
            }

            if (expectContinue)
            {
                DebugLog.Write("Sending 100 Continue");
                var continueBytes = Encoding.UTF8.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
                await stream.WriteAsync(continueBytes);
            }

            OnLog?.Invoke($"{method} {rawPath} <- {remoteIp}");

            // 跟踪已连接设备（仅输入正确连接码后）
            if (code == _connectionCode)
                TrackDevice(remoteIp, userAgent);

            try
            {
                if (path == "/" && method == "GET")
                {
                    if (string.IsNullOrEmpty(code))
                        await SendHtmlAsync(stream, HtmlTemplates.EnterCodePage);
                    else if (code != _connectionCode)
                        await SendHtmlAsync(stream, HtmlTemplates.ErrorPage("连接码错误"), "403 Forbidden");
                    else
                        await SendHtmlAsync(stream, HtmlTemplates.MainPage(_connectionCode));
                }
                else if (code != _connectionCode)
                {
                    await SendJsonAsync(stream, "{\"error\":\"连接码错误\"}", "403 Forbidden");
                }
                else if (path.StartsWith("/download/") && method == "GET")
                {
                    var fileId = path["/download/".Length..];
                    await HandleDownloadAsync(client, stream, fileId, remoteIp, rangeHeader);
                }
                else if (path == "/upload" && method == "POST")
                {
                    await HandleUploadAsync(stream, bodyStream, contentType, contentLength, remoteIp);
                }
                else if (path == "/files" && method == "GET")
                {
                    await HandleFileListAsync(stream);
                }
                else if (path == "/events" && method == "GET")
                {
                    await HandleSseAsync(client, stream);
                }
                else
                {
                    await SendHtmlAsync(stream, HtmlTemplates.ErrorPage("页面不存在"), "404 Not Found");
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"请求处理错误: {ex.Message}\n{ex.StackTrace}");
                try
                {
                    await SendJsonAsync(stream, "{\"error\":\"" + ex.Message.Replace("\"", "'").Replace("\n", " ") + "\"}", "500 Internal Server Error");
                }
                catch { }
            }
        }
    }

    private static async Task<(List<byte> Headers, byte[] Leftover, int LeftoverCount)> ReadHeadersAsync(NetworkStream stream)
    {
        var result = new List<byte>(1024);
        var readBuffer = new byte[4096];
        int crlfCount = 0;
        int totalRead = 0;
        int headerEndIndex = -1;

        while (headerEndIndex < 0)
        {
            var bytesRead = await stream.ReadAsync(readBuffer, totalRead, readBuffer.Length - totalRead);
            DebugLog.Write($"ReadHeaders: read {bytesRead} bytes, totalRead={totalRead + bytesRead}");
            if (bytesRead == 0) return (result, Array.Empty<byte>(), 0);
            totalRead += bytesRead;

            for (int i = totalRead - bytesRead; i < totalRead; i++)
            {
                if ((crlfCount % 2 == 0 && readBuffer[i] == '\r') || (crlfCount % 2 == 1 && readBuffer[i] == '\n'))
                    crlfCount++;
                else
                    crlfCount = readBuffer[i] == '\r' ? 1 : 0;

                if (crlfCount >= 4)
                {
                    headerEndIndex = i;
                    break;
                }
            }
        }

        // 提取 header 字节
        for (int i = 0; i <= headerEndIndex; i++)
            result.Add(readBuffer[i]);

        // header 之后的字节是 body 的开头，必须回传
        int leftoverCount = totalRead - headerEndIndex - 1;
        var leftover = new byte[leftoverCount];
        if (leftoverCount > 0)
            Buffer.BlockCopy(readBuffer, headerEndIndex + 1, leftover, 0, leftoverCount);

        return (result, leftover, leftoverCount);
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query)) return dict;

        foreach (var pair in query.Split('&'))
        {
            var eqIdx = pair.IndexOf('=');
            if (eqIdx > 0)
                dict[Uri.UnescapeDataString(pair[..eqIdx])] = Uri.UnescapeDataString(pair[(eqIdx + 1)..]);
        }
        return dict;
    }

    private async Task HandleDownloadAsync(TcpClient client, NetworkStream stream, string fileId, string remoteIp, string? rangeHeader)
    {
        await _concurrencyLimiter.WaitAsync();
        try
        {
        if (!_sharedFiles.TryGetValue(fileId, out var file))
        {
            await SendHtmlAsync(stream, HtmlTemplates.ErrorPage("文件不存在或已过期"), "404 Not Found");
            return;
        }

        if (!File.Exists(file.FilePath))
        {
            await SendHtmlAsync(stream, HtmlTemplates.ErrorPage("源文件已被删除"), "404 Not Found");
            return;
        }

        var realSize = new FileInfo(file.FilePath).Length;
        var fileName = Uri.EscapeDataString(file.FileName);

        // 解析 Range 请求
        long rangeStart = 0, rangeEnd = realSize - 1;
        bool isRangeRequest = false;

        if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
        {
            var rangeSpec = rangeHeader["bytes=".Length..];
            var parts = rangeSpec.Split('-', 2);
            if (parts.Length == 2)
            {
                if (long.TryParse(parts[0], out var start))
                {
                    rangeStart = start;
                    isRangeRequest = true;
                    if (!string.IsNullOrEmpty(parts[1]) && long.TryParse(parts[1], out var end))
                        rangeEnd = Math.Min(end, realSize - 1);
                }
                else if (!string.IsNullOrEmpty(parts[1]) && long.TryParse(parts[1], out var suffixLen))
                {
                    // bytes=-N 表示最后 N 个字节
                    rangeStart = Math.Max(0, realSize - suffixLen);
                    isRangeRequest = true;
                }
            }
        }

        if (isRangeRequest)
        {
            if (rangeStart >= realSize)
            {
                await SendJsonAsync(stream, "{\"error\":\"请求范围越界\"}", "416 Range Not Satisfiable");
                return;
            }

            var contentLen = rangeEnd - rangeStart + 1;
            OnLog?.Invoke($"断点续传: {file.FileName} ({FormatSize(contentLen)}) bytes={rangeStart}-{rangeEnd}/{realSize} <- {remoteIp}");

            var header = $"HTTP/1.1 206 Partial Content\r\nContent-Type: application/octet-stream\r\nContent-Length: {contentLen}\r\nContent-Range: bytes {rangeStart}-{rangeEnd}/{realSize}\r\nAccept-Ranges: bytes\r\nContent-Disposition: attachment; filename*=''UTF-8''{fileName}\r\nConnection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(header));

            await using var fs = new FileStream(file.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous);
            fs.Seek(rangeStart, SeekOrigin.Begin);
            var buffer = new byte[1024 * 1024];
            long remaining = contentLen;
            while (remaining > 0)
            {
                var toRead = (int)Math.Min(buffer.Length, remaining);
                var bytesRead = await fs.ReadAsync(buffer.AsMemory(0, toRead));
                if (bytesRead == 0) break;
                await stream.WriteAsync(buffer.AsMemory(0, bytesRead));
                remaining -= bytesRead;
            }
            await stream.FlushAsync();

            OnLog?.Invoke($"续传完成: {file.FileName}");
        }
        else
        {
            OnLog?.Invoke($"开始下载: {file.FileName} ({FormatSize(realSize)}) <- {remoteIp}");

            var header = $"HTTP/1.1 200 OK\r\nContent-Type: application/octet-stream\r\nContent-Length: {realSize}\r\nAccept-Ranges: bytes\r\nContent-Disposition: attachment; filename*=''UTF-8''{fileName}\r\nConnection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(header));

            await using var fs = new FileStream(file.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous);
            var buffer = new byte[1024 * 1024];
            long totalSent = 0;
            int bytesRead;
            while ((bytesRead = await fs.ReadAsync(buffer)) > 0)
            {
                await stream.WriteAsync(buffer.AsMemory(0, bytesRead));
                totalSent += bytesRead;
            }
            await stream.FlushAsync();

            OnLog?.Invoke($"下载完成: {file.FileName}, 发送={totalSent}字节, 匹配={totalSent == realSize}");
        }

        try { client.Client.Shutdown(SocketShutdown.Send); } catch { }

        OnTransferCompleted?.Invoke(new TransferRecord(file.FileName, realSize, TransferDirection.Download, DateTime.Now, remoteIp));

        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    private async Task HandleUploadAsync(NetworkStream responseStream, Stream bodyStream, string? contentType, long contentLength, string remoteIp)
    {
        OnLog?.Invoke($"上传请求: {remoteIp}, CL={contentLength}, CT={contentType}");

        if (contentLength == 0 || string.IsNullOrEmpty(contentType))
        {
            await SendJsonAsync(responseStream, "{\"error\":\"没有文件数据\"}", "400 Bad Request");
            return;
        }

        if (!contentType.Contains("multipart/form-data"))
        {
            await SendJsonAsync(responseStream, "{\"error\":\"请使用 multipart/form-data 格式\"}", "400 Bad Request");
            return;
        }

        var boundary = GetBoundary(contentType);
        if (string.IsNullOrEmpty(boundary))
        {
            await SendJsonAsync(responseStream, "{\"error\":\"无法解析上传边界\"}", "400 Bad Request");
            return;
        }

        using var limitedBodyStream = new ReadLimitStream(bodyStream, contentLength);
        using var multipartReader = new MultipartReader(boundary, limitedBodyStream);

        OnLog?.Invoke($"解析上传数据, boundary={boundary}");

        var section = await multipartReader.ReadNextSectionAsync();
        if (section == null)
        {
            OnLog?.Invoke("未解析到 section，返回错误");
            await SendJsonAsync(responseStream, "{\"error\":\"未解析到文件数据\"}", "400 Bad Request");
            return;
        }
        while (section != null)
        {
            if (section.ContentDisposition?.Contains("filename") == true)
            {
                var fileName = GetFileName(section.ContentDisposition);
                if (string.IsNullOrEmpty(fileName))
                    fileName = $"upload_{DateTime.Now:yyyyMMdd_HHmmss}";

                fileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));

                var savePath = Path.Combine(_saveDirectory, fileName);
                if (File.Exists(savePath))
                {
                    var name = Path.GetFileNameWithoutExtension(fileName);
                    var ext = Path.GetExtension(fileName);
                    savePath = Path.Combine(_saveDirectory, $"{name}_{DateTime.Now:HHmmss}{ext}");
                }

                OnLog?.Invoke($"开始接收: {fileName} <- {remoteIp}, Content-Length={contentLength}");

                long totalSize = 0;
                await using var fs = new FileStream(savePath, FileMode.Create, FileAccess.Write);
                var buffer = new byte[81920];
                int bytesRead;
                int readCount = 0;
                while ((bytesRead = await section.Body.ReadAsync(buffer)) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalSize += bytesRead;
                    readCount++;
                    if (readCount <= 3 || readCount % 100 == 0)
                        DebugLog.Write($"上传读取 #{readCount}: {bytesRead} bytes, 累计 {totalSize} bytes");
                }
                DebugLog.Write($"上传完成: {fileName}, 读取次数={readCount}, 总大小={totalSize} bytes, Content-Length={contentLength}");

                var finalName = Path.GetFileName(savePath);
                OnTransferCompleted?.Invoke(new TransferRecord(finalName, totalSize, TransferDirection.Upload, DateTime.Now, remoteIp));
                OnFileReceived?.Invoke(finalName, totalSize);
                OnLog?.Invoke($"接收完成: {finalName} ({FormatSize(totalSize)})");

                await SendJsonAsync(responseStream, "{\"success\":true,\"fileName\":\"" + finalName + "\",\"size\":" + totalSize + "}");
                return;
            }
            section = await multipartReader.ReadNextSectionAsync();
        }

        await SendJsonAsync(responseStream, "{\"error\":\"未找到文件\"}", "400 Bad Request");
    }

    private async Task HandleFileListAsync(NetworkStream stream)
    {
        var files = _sharedFiles.Select(f => new
        {
            id = f.Key,
            name = f.Value.FileName,
            size = FormatSize(f.Value.FileSize)
        });

        var json = System.Text.Json.JsonSerializer.Serialize(files);
        await SendJsonAsync(stream, json);
    }

    private async Task HandleSseAsync(TcpClient client, NetworkStream stream)
    {
        var clientId = Guid.NewGuid().ToString("N")[..8];
        var remoteIp = ((IPEndPoint?)client.Client.RemoteEndPoint)?.Address.ToString() ?? "unknown";
        var header = "HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(header));
        await stream.FlushAsync();

        var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
        _sseClients[clientId] = writer;

        OnLog?.Invoke($"SSE 客户端已连接: {clientId}");

        // 发送当前文件列表
        var json = System.Text.Json.JsonSerializer.Serialize(GetSharedFiles().Select(f => new { id = f.FileId, name = f.FileName, size = FormatSize(f.FileSize) }));
        await writer.WriteAsync($"data: {json}\n\n");
        await stream.FlushAsync();

        // 保持连接直到客户端断开
        try
        {
            var buffer = new byte[1];
            while (client.Connected && await stream.ReadAsync(buffer) > 0) { }
        }
        catch { }

        _sseClients.TryRemove(clientId, out _);

        // 设备断开，移除并通知
        if (_connectedDevices.TryRemove(remoteIp, out _))
        {
            OnDevicesChanged?.Invoke(GetConnectedDevices());
            OnLog?.Invoke($"设备已断开: {remoteIp}");
        }
    }

    private static string? GetBoundary(string contentType)
    {
        var elements = contentType.Split(';', StringSplitOptions.TrimEntries);
        foreach (var element in elements)
        {
            if (element.StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))
                return element["boundary=".Length..].Trim('"');
        }
        return null;
    }

    private static string? GetFileName(string contentDisposition)
    {
        var parts = contentDisposition.Split(';', StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            if (part.StartsWith("filename=", StringComparison.OrdinalIgnoreCase))
            {
                var val = part["filename=".Length..].Trim('"');
                return HttpUtility.UrlDecode(val);
            }
            if (part.StartsWith("filename*=", StringComparison.OrdinalIgnoreCase))
            {
                var val = part["filename*=".Length..];
                var idx = val.IndexOf("''", StringComparison.Ordinal);
                if (idx >= 0)
                    return HttpUtility.UrlDecode(val[(idx + 2)..]);
                return HttpUtility.UrlDecode(val.Trim('"'));
            }
        }
        return null;
    }

    private static async Task SendHtmlAsync(NetworkStream stream, string html, string status = "200 OK")
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        var header = $"HTTP/1.1 {status}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nCache-Control: no-cache, no-store, must-revalidate\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(header));
        await stream.WriteAsync(bytes);
    }

    private static async Task SendJsonAsync(NetworkStream stream, string json, string status = "200 OK")
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var header = $"HTTP/1.1 {status}\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(header));
        await stream.WriteAsync(bytes);
    }

    private static string FormatSize(long bytes) => FormatHelper.FormatSize(bytes);

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}

/// <summary>
/// 限制读取长度的流包装，用于从 NetworkStream 中精确读取 HTTP body。
/// </summary>
internal class ReadLimitStream : Stream
{
    private readonly Stream _inner;
    private long _remaining;

    public ReadLimitStream(Stream inner, long totalLength)
    {
        _inner = inner;
        _remaining = totalLength;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_remaining <= 0) return 0;
        var toRead = (int)Math.Min(count, _remaining);
        var read = _inner.Read(buffer, offset, toRead);
        _remaining -= read;
        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        if (_remaining <= 0) return 0;
        var toRead = (int)Math.Min(count, _remaining);
        var read = await _inner.ReadAsync(buffer.AsMemory(offset, toRead), ct);
        _remaining -= read;
        return read;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _remaining;
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>
/// 在底层流之前插入前缀字节。先读前缀，再读底层流。
/// </summary>
internal class PrefixedStream : Stream
{
    private readonly Stream _inner;
    private byte[] _prefix;
    private int _prefixPos;
    private int _prefixLen;

    public PrefixedStream(Stream inner, byte[] prefix, int prefixLen)
    {
        _inner = inner;
        _prefix = prefix;
        _prefixLen = prefixLen;
        _prefixPos = 0;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_prefixPos < _prefixLen)
        {
            var toCopy = Math.Min(count, _prefixLen - _prefixPos);
            Buffer.BlockCopy(_prefix, _prefixPos, buffer, offset, toCopy);
            _prefixPos += toCopy;
            return toCopy;
        }
        return _inner.Read(buffer, offset, count);
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        if (_prefixPos < _prefixLen)
        {
            var toCopy = Math.Min(count, _prefixLen - _prefixPos);
            Buffer.BlockCopy(_prefix, _prefixPos, buffer, offset, toCopy);
            _prefixPos += toCopy;
            return toCopy;
        }
        return await _inner.ReadAsync(buffer.AsMemory(offset, count), ct);
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
