using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanTransfer.Services;
using QRCoder;

namespace LanTransfer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private HttpServerService? _server;
    private Task? _serverTask;
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "LanTransfer", "TempZip");

    [ObservableProperty] private string _localIp = "";
    [ObservableProperty] private int _port;
    [ObservableProperty] private string _connectionCode = "";
    [ObservableProperty] private string _connectionUrl = "";
    [ObservableProperty] private BitmapImage? _qrCodeImage;
    [ObservableProperty] private string _statusText = "未启动";
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _logText = "";

    public ObservableCollection<TransferRecord> TransferRecords { get; } = new();
    public ObservableCollection<SharedFileInfo> SentFiles { get; } = new();
    public ObservableCollection<SharedFileInfo> ReceivedFiles { get; } = new();
    public ObservableCollection<ConnectedDevice> ConnectedDevices { get; } = new();

    [ObservableProperty] private string _deviceCountText = "已连接设备: 0";

    public void Initialize()
    {
        LocalIp = NetworkHelper.GetLocalIpAddress();
        Port = NetworkHelper.FindAvailablePort();
        ConnectionCode = NetworkHelper.GenerateConnectionCode();
        ConnectionUrl = $"http://{LocalIp}:{Port}";

        // 确保防火墙放行
        if (!NetworkHelper.EnsureFirewallRule(Port))
            AppendLog("防火墙规则添加失败，请手动放行端口或以管理员身份运行");

        GenerateQrCode(ConnectionUrl);
        StartServer();
    }

    private void GenerateQrCode(string url)
    {
        try
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
            using var qrCode = new PngByteQRCode(qrCodeData);
            var qrCodeBytes = qrCode.GetGraphic(10);

            using var ms = new MemoryStream(qrCodeBytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();
            QrCodeImage = image;
        }
        catch (Exception ex)
        {
            AppendLog($"二维码生成失败: {ex.Message}");
        }
    }

    private void StartServer()
    {
        _server = new HttpServerService(ConnectionCode);
        _server.OnTransferCompleted += record =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                TransferRecords.Insert(0, record);
            });
        };
        _server.OnFileReceived += (fileName, fileSize) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ReceivedFiles.Insert(0, new SharedFileInfo
                {
                    FileId = "",
                    FileName = fileName,
                    FileSize = fileSize
                });
            });
        };
        _server.OnLog += msg =>
        {
            Application.Current.Dispatcher.Invoke(() => AppendLog(msg));
        };
        _server.OnDevicesChanged += devices =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ConnectedDevices.Clear();
                foreach (var d in devices) ConnectedDevices.Add(d);
                DeviceCountText = $"已连接设备: {devices.Count}";
            });
        };

        _serverTask = Task.Run(async () =>
        {
            try
            {
                await _server.StartAsync(LocalIp, Port);
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    AppendLog($"服务器错误: {ex.Message}");
                    StatusText = "启动失败";
                    IsRunning = false;
                });
            }
        });

        IsRunning = true;
        StatusText = "运行中";
        NetworkHelper.PreventSleep();
    }

    [RelayCommand]
    private void OpenSaveFolder()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "LanTransfer", "Received");
        Directory.CreateDirectory(dir);
        System.Diagnostics.Process.Start("explorer.exe", dir);
    }

    [RelayCommand]
    private void CopyUrl()
    {
        try
        {
            Clipboard.SetText(ConnectionUrl);
            AppendLog("连接地址已复制到剪贴板");
        }
        catch { }
    }

    [RelayCommand]
    private void CopyCode()
    {
        try
        {
            Clipboard.SetText(ConnectionCode);
            AppendLog("连接码已复制到剪贴板");
        }
        catch { }
    }

    public void ShareFile(string filePath)
    {
        if (_server == null) return;

        if (Directory.Exists(filePath))
        {
            Task.Run(() => ShareFolderAsync(filePath));
            return;
        }

        if (!File.Exists(filePath))
        {
            AppendLog($"文件不存在: {filePath}");
            return;
        }

        ShareSingleFile(filePath);
    }

    private void ShareSingleFile(string filePath)
    {
        var fileId = _server!.AddSharedFile(filePath);
        var fileName = Path.GetFileName(filePath);
        var fileInfo = new FileInfo(filePath);

        Application.Current.Dispatcher.Invoke(() =>
        {
            SentFiles.Insert(0, new SharedFileInfo
            {
                FileId = fileId,
                FileName = fileName,
                FileSize = fileInfo.Length
            });
        });

        AppendLog($"已共享: {fileName} ({FormatSize(fileInfo.Length)})");
    }

    private async Task ShareFolderAsync(string folderPath)
    {
        var folderName = Path.GetFileName(folderPath);
        Directory.CreateDirectory(_tempDir);

        var zipFileName = $"{folderName}_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
        var zipPath = Path.Combine(_tempDir, zipFileName);

        AppendLog($"正在压缩文件夹: {folderName}...");

        try
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
            await Task.Run(() => ZipFile.CreateFromDirectory(folderPath, zipPath, CompressionLevel.Fastest, false));
        }
        catch (Exception ex)
        {
            AppendLog($"压缩失败: {ex.Message}");
            return;
        }

        var zipInfo = new FileInfo(zipPath);
        var fileId = _server!.AddSharedFile(zipPath);

        Application.Current.Dispatcher.Invoke(() =>
        {
            SentFiles.Insert(0, new SharedFileInfo
            {
                FileId = fileId,
                FileName = zipFileName,
                FileSize = zipInfo.Length
            });
        });

        AppendLog($"已共享: {zipFileName} ({FormatSize(zipInfo.Length)})");
    }

    public void ShareFiles(string[] filePaths)
    {
        Task.Run(() =>
        {
            foreach (var path in filePaths)
            {
                ShareFile(path);
            }
        });
    }

    [RelayCommand]
    private void UnshareFile(SharedFileInfo? fileInfo)
    {
        if (fileInfo == null || _server == null) return;
        if (_server.RemoveSharedFile(fileInfo.FileId))
        {
            SentFiles.Remove(fileInfo);
            AppendLog($"已取消共享: {fileInfo.FileName}");
        }
    }

    [RelayCommand]
    private void ClearAllShared()
    {
        if (_server == null) return;
        foreach (var file in _server.GetSharedFiles())
        {
            _server.RemoveSharedFile(file.FileId);
        }
        SentFiles.Clear();
        AppendLog("已清空所有共享文件");
    }

    private void AppendLog(string message)
    {
        LogText = $"[{DateTime.Now:HH:mm:ss}] {message}\n" + LogText;
        if (LogText.Length > 5000)
            LogText = LogText[..5000];
    }

    private static string FormatSize(long bytes) => FormatHelper.FormatSize(bytes);

    public async Task ShutdownAsync()
    {
        _server?.Stop();
        if (_serverTask != null)
        {
            try { await _serverTask; } catch { }
        }
        _server?.Dispose();
        NetworkHelper.AllowSleep();

        // 清理临时 ZIP 文件
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch { }
    }
}
