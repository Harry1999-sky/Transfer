using System.IO;
using System.Text.Json;
using System.Windows;

namespace LanTransfer.Localization;

public static class LanguageManager
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LanTransfer", "settings.json");

    private static ResourceDictionary? _activeDict;
    private static readonly string[] SupportedLanguages = { "zh-CN", "en" };

    public static string CurrentLanguage { get; private set; } = "zh-CN";

    public static event Action? LanguageChanged;

    private static readonly Uri ZhUri = new("pack://application:,,,/Localization/Strings.zh-CN.xaml", UriKind.Absolute);
    private static readonly Uri EnUri = new("pack://application:,,,/Localization/Strings.en.xaml", UriKind.Absolute);

    public static void Load()
    {
        var lang = ReadSettings();
        if (string.IsNullOrEmpty(lang) || !SupportedLanguages.Contains(lang))
            lang = "zh-CN";

        CurrentLanguage = lang;
        _activeDict = new ResourceDictionary { Source = lang == "en" ? EnUri : ZhUri };
        Application.Current.Resources.MergedDictionaries.Add(_activeDict);
    }

    public static void SetLanguage(string lang)
    {
        if (lang == CurrentLanguage || _activeDict == null) return;
        CurrentLanguage = lang;
        SaveSettings(lang);

        try
        {
            _activeDict.Source = lang == "en" ? EnUri : ZhUri;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Step1 - Set Source failed:\n\n{ex}", "Debug", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            Application.Current.Dispatcher.BeginInvoke(
                () =>
                {
                    try
                    {
                        LanguageChanged?.Invoke();
                    }
                    catch (Exception ex2)
                    {
                        MessageBox.Show($"Step2 - LanguageChanged failed:\n\n{ex2}", "Debug", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                },
                System.Windows.Threading.DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Step2 - BeginInvoke failed:\n\n{ex}", "Debug", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public static string GetString(string key)
    {
        if (_activeDict != null && _activeDict.Contains(key))
            return _activeDict[key]?.ToString() ?? key;
        return key;
    }

    private static string? ReadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("language", out var langEl))
                    return langEl.GetString();
            }
        }
        catch { }
        return null;
    }

    private static void SaveSettings(string lang)
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new { language = lang }));
        }
        catch { }
    }
}
