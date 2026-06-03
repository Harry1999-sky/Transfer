using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using LanTransfer.Localization;
using LanTransfer.Services;
using LanTransfer.ViewModels;
using Microsoft.Win32;

namespace LanTransfer;

public partial class MainWindow : Window
{
    private static readonly SolidColorBrush DefaultBorderBrush = new(Color.FromRgb(0x66, 0x7e, 0xea));
    private static readonly SolidColorBrush DragBorderBrush = Brushes.MediumPurple;

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;

        // 从 exe 文件加载图标（单文件打包不支持 XAML 中直接引用资源图标）
        try
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (exePath != null)
                Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    System.Drawing.Icon.ExtractAssociatedIcon(exePath).Handle,
                    Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
        }
        catch { }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.Initialize();

            var hwnd = new WindowInteropHelper(this).Handle;
            DragAcceptFiles(hwnd, true);

            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{LanguageManager.GetString("InitFailed")}: {ex.Message}\n\n{ex.StackTrace}",
                LanguageManager.GetString("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_DROPFILES = 0x0233;

        if (msg == WM_DROPFILES)
        {
            DropZone.BorderBrush = DefaultBorderBrush;

            var hDrop = wParam;
            var fileCount = DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
            var files = new string[fileCount];

            for (uint i = 0; i < fileCount; i++)
            {
                var length = DragQueryFile(hDrop, i, null, 0);
                var sb = new System.Text.StringBuilder((int)length + 1);
                DragQueryFile(hDrop, i, sb, (uint)sb.Capacity);
                files[i] = sb.ToString();
            }

            DragFinish(hDrop);

            if (files.Length > 0)
                ViewModel.ShareFiles(files);

            handled = true;
        }

        return IntPtr.Zero;
    }

    private void DropZone_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var choice = MessageBox.Show(
            LanguageManager.GetString("SelectFolderOrFile"),
            LanguageManager.GetString("SelectType"),
            MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

        if (choice == MessageBoxResult.Yes)
        {
            var folderPath = ShowFolderBrowserDialog();
            if (folderPath != null)
                ViewModel.ShareFile(folderPath);
        }
        else if (choice == MessageBoxResult.No)
        {
            var dialog = new OpenFileDialog
            {
                Title = LanguageManager.GetString("SelectFilesTitle"),
                Multiselect = true
            };
            if (dialog.ShowDialog() == true)
                ViewModel.ShareFiles(dialog.FileNames);
        }
    }

    private static string? ShowFolderBrowserDialog()
    {
        var dialog = (IFileOpenDialog)new FileOpenDialog();
        dialog.GetOptions(out var options);
        dialog.SetOptions(options | FOS.FOS_PICKFOLDERS | FOS.FOS_FORCEFILESYSTEM);

        if (dialog.Show(IntPtr.Zero) == 0)
        {
            dialog.GetResult(out var result);
            result.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var path);
            return path;
        }
        return null;
    }

    private void UrlText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ViewModel.CopyUrlCommand.Execute(null);
    }

    private void CodeText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ViewModel.CopyCodeCommand.Execute(null);
    }

    private void OpenFolder_Click(object sender, MouseButtonEventArgs e)
    {
        ViewModel.OpenSaveFolderCommand.Execute(null);
    }

    private void UnshareFile_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is SharedFileInfo fileInfo)
            ViewModel.UnshareFileCommand.Execute(fileInfo);
    }

    private void LanguageToggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var newLang = LanguageManager.CurrentLanguage == "zh-CN" ? "en" : "zh-CN";
            LanguageManager.SetLanguage(newLang);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Language switch error:\n\n{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Closing -= Window_Closing;
        LanguageManager.IsShuttingDown = true;
        await ViewModel.ShutdownAsync();
        Close();
    }

    #region Win32 API

    [DllImport("shell32.dll")]
    private static extern void DragAcceptFiles(IntPtr hwnd, bool fAccept);

    [DllImport("shell32.dll")]
    private static extern uint DragQueryFile(IntPtr hDrop, uint iFile, System.Text.StringBuilder? lpszFile, uint cch);

    [DllImport("shell32.dll")]
    private static extern void DragFinish(IntPtr hDrop);

    [ComImport, Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
    private class FileOpenDialog { }

    [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig] int Show(IntPtr hwndOwner);
        void SetFileTypes();
        void SetFileTypeIndex();
        void GetFileTypeIndex();
        void Advise();
        void Unadvise();
        void SetOptions(FOS fos);
        void GetOptions(out FOS fos);
        void SetDefaultFolder();
        void SetFolder(IShellItem psi);
        void GetFolder(out IShellItem ppsi);
        void GetCurrentSelection(out IShellItem ppsi);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult(out IShellItem ppsi);
        void AddPlace(IShellItem psi, int fdap);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close(int hr);
        void SetClientGuid();
        void ClearClientData();
        void SetFilter();
        void GetResults();
        void GetSelectedItems();
    }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler();
        void GetParent();
        void GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes();
        void Compare();
    }

    [Flags]
    private enum FOS
    {
        FOS_PICKFOLDERS = 0x20,
        FOS_FORCEFILESYSTEM = 0x40
    }

    private enum SIGDN : uint
    {
        SIGDN_FILESYSPATH = 0x80058000
    }

    #endregion
}
