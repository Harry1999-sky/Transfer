using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanTransfer.Localization;
using LanTransfer.Services;
using QRCoder;

namespace LanTransfer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private HttpServerService? _server;
    private Task? _serverTask;
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "LanTransfer", "TempZip");

    // 状态追踪，用于语言切换时重新翻译
    private enum AppState { NotRunning, Running, StartFailed }
    private AppState _currentState = AppState.NotRunning;
    private int _deviceCount;

    [ObservableProperty] private string _localIp = "";
    [ObservableProperty] private int _port;
    [ObservableProperty] private string _connectionCode = "";
    [ObservableProperty] private string _connectionUrl = "";
    [ObservableProperty] private BitmapImage? _qrCodeImage;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _logText = "";

    public ObservableCollection<TransferRecord> TransferRecords { get; } = new();
    public ObservableCollection<SharedFileInfo> SentFiles { get; } = new();
    public ObservableCollection<SharedFileInfo> ReceivedFiles { get; } = new();
    public ObservableCollection<ConnectedDevice> ConnectedDevices { get; } = new();

    [ObservableProperty] private string _deviceCountText = "";

    public MainViewModel()
    {
        _statusText = LanguageManager.GetString("StatusNotStarted");
        _deviceCountText = string.Format(LanguageManager.GetString("DeviceCountText"), 0);
        LanguageManager.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        StatusText = _currentState switch
        {
            AppState.NotRunning => LanguageManager.GetString("StatusNotStarted"),
            AppState.Running => LanguageManager.GetString("StatusRunning"),
            AppState.StartFailed => LanguageManager.GetString("StatusStartFailed"),
            _ => StatusText
        };
        DeviceCountText = string.Format(LanguageManager.GetString("DeviceCountText"), _deviceCount);
    }

    public void Initialize()
    {
        LocalIp = NetworkHelper.GetLocalIpAddress();
        Port = NetworkHelper.FindAvailablePort();
        ConnectionCode = NetworkHelper.GenerateConnectionCode();
        ConnectionUrl = $"http://{LocalIp}:{Port}";

        // 确保防火墙放行
        if (!NetworkHelper.EnsureFirewallRule(Port))
            AppendLog(LanguageManager.GetString("FirewallFailed"));

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
            AppendLog(string.Format(LanguageManager.GetString("QrCodeFailed"), ex.Message));
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
                _deviceCount = devices.Count;
                DeviceCountText = string.Format(LanguageManager.GetString("DeviceCountText"), _deviceCount);
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
                    AppendLog(string.Format(LanguageManager.GetString("ServerError"), ex.Message));
                    _currentState = AppState.StartFailed;
                    StatusText = LanguageManager.GetString("StatusStartFailed");
                    IsRunning = false;
                });
            }
        });

        IsRunning = true;
        _currentState = AppState.Running;
        StatusText = LanguageManager.GetString("StatusRunning");
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
            AppendLog(LanguageManager.GetString("UrlCopied"));
        }
        catch { }
    }

    [RelayCommand]
    private void CopyCode()
    {
        try
        {
            Clipboard.SetText(ConnectionCode);
            AppendLog(LanguageManager.GetString("CodeCopied"));
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
            AppendLog(string.Format(LanguageManager.GetString("FileNotFound"), filePath));
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

        AppendLog(string.Format(LanguageManager.GetString("FileShared"), fileName, FormatSize(fileInfo.Length)));
    }

    private async Task ShareFolderAsync(string folderPath)
    {
        var folderName = Path.GetFileName(folderPath);
        Directory.CreateDirectory(_tempDir);

        var zipFileName = $"{folderName}_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
        var zipPath = Path.Combine(_tempDir, zipFileName);

        AppendLog(string.Format(LanguageManager.GetString("CompressingFolder"), folderName));

        try
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
            await Task.Run(() => ZipFile.CreateFromDirectory(folderPath, zipPath, CompressionLevel.Fastest, false));
        }
        catch (Exception ex)
        {
            AppendLog(string.Format(LanguageManager.GetString("CompressFailed"), ex.Message));
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

        AppendLog(string.Format(LanguageManager.GetString("FileShared"), zipFileName, FormatSize(zipInfo.Length)));
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
            AppendLog(string.Format(LanguageManager.GetString("Unshared"), fileInfo.FileName));
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
        AppendLog(LanguageManager.GetString("AllCleared"));
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
        LanguageManager.LanguageChanged -= OnLanguageChanged;
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
