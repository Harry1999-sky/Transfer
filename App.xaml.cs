using System.Windows;
using LanTransfer.Localization;

namespace LanTransfer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 捕获所有未处理异常，防止静默崩溃
        DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show($"Unhandled exception:\n\n{args.Exception}", "Crash", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            MessageBox.Show($"Domain exception:\n\n{ex}", "Crash", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        LanguageManager.Load();
    }
}
