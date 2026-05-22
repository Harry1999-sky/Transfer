using System.IO;
using System.Text;

namespace LanTransfer.Services;

public class MultipartReader : IDisposable
{
    private readonly string _boundary;
    private readonly Stream _stream;
    private byte[]? _buffer;
    private int _bufferLen;
    private int _bufferPos;

    public MultipartReader(string boundary, Stream stream)
    {
        _boundary = boundary;
        _stream = stream;
    }

    public async Task<MultipartSection?> ReadNextSectionAsync()
    {
        if (_buffer == null)
        {
            _buffer = new byte[81920];
            _bufferLen = await _stream.ReadAsync(_buffer);
            _bufferPos = 0;
        }

        var boundaryBytes = Encoding.UTF8.GetBytes("--" + _boundary);
        var found = await SkipToBoundaryAsync(boundaryBytes);
        if (!found) return null;

        await ReadLineAsync();

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            var line = await ReadLineAsync();
            if (string.IsNullOrEmpty(line)) break;

            var colonIdx = line.IndexOf(':');
            if (colonIdx > 0)
            {
                var key = line[..colonIdx].Trim();
                var value = line[(colonIdx + 1)..].Trim();
                headers[key] = value;
            }
        }

        headers.TryGetValue("Content-Disposition", out var contentDisposition);
        headers.TryGetValue("Content-Type", out var contentType);

        return new MultipartSection(this, contentDisposition, contentType);
    }

    private async Task<bool> SkipToBoundaryAsync(byte[] boundary)
    {
        var matchPos = 0;
        while (true)
        {
            if (_bufferPos >= _bufferLen)
            {
                _bufferLen = await _stream.ReadAsync(_buffer!);
                _bufferPos = 0;
                if (_bufferLen == 0) return false;
            }

            var b = _buffer![_bufferPos++];
            if (b == boundary[matchPos])
            {
                matchPos++;
                if (matchPos == boundary.Length) return true;
            }
            else
            {
                matchPos = 0;
            }
        }
    }

    private async Task<string> ReadLineAsync()
    {
        var sb = new StringBuilder();
        var prev = (byte)0;
        while (true)
        {
            if (_bufferPos >= _bufferLen)
            {
                _bufferLen = await _stream.ReadAsync(_buffer!);
                _bufferPos = 0;
                if (_bufferLen == 0) break;
            }

            var b = _buffer![_bufferPos++];
            if (b == '\n' && prev == '\r')
            {
                sb.Remove(sb.Length - 1, 1);
                break;
            }

            sb.Append((char)b);
            prev = b;
        }
        return sb.ToString();
    }

    internal int Read(byte[] buffer, int offset, int count)
    {
        while (true)
        {
            var available = _bufferLen - _bufferPos;
            if (available > 0)
            {
                var toCopy = Math.Min(available, count);
                Buffer.BlockCopy(_buffer!, _bufferPos, buffer, offset, toCopy);
                _bufferPos += toCopy;
                return toCopy;
            }

            // 缓冲区已空，从流中重新填充
            _bufferLen = _stream.Read(_buffer!, 0, _buffer!.Length);
            _bufferPos = 0;
            if (_bufferLen == 0) return 0;
        }
    }

    public void Dispose()
    {
        _buffer = null;
    }
}

public class MultipartSection
{
    private readonly MultipartReader _reader;

    public string? ContentDisposition { get; }
    public string? ContentType { get; }
    public Stream Body { get; }

    public MultipartSection(MultipartReader reader, string? contentDisposition, string? contentType)
    {
        _reader = reader;
        ContentDisposition = contentDisposition;
        ContentType = contentType;
        Body = new SectionStream(reader);
    }
}

internal class SectionStream : Stream
{
    private readonly MultipartReader _reader;

    public SectionStream(MultipartReader reader) => _reader = reader;

    public override int Read(byte[] buffer, int offset, int count) => _reader.Read(buffer, offset, count);
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        return await Task.Run(() => _reader.Read(buffer, offset, count), ct);
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
