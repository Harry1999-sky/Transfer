using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using LanTransfer.Services;

namespace LanTransfer;

internal static class FormatHelper
{
    public static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}

public class DirectionToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TransferDirection direction)
            return direction == TransferDirection.Upload ? "" : ""; // Upload / Download arrows
        return ""; // Sync
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class DirectionToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TransferDirection direction)
            return direction == TransferDirection.Upload
                ? new SolidColorBrush(Color.FromRgb(0x4F, 0x6B, 0xED))  // accent blue
                : new SolidColorBrush(Color.FromRgb(0x2D, 0xB8, 0x4B)); // success green
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class FileSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is long bytes ? FormatHelper.FormatSize(bytes) : "0 B";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
            return count > 0 ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// 根据设备名称返回 Fluent System Icons 字符。
/// </summary>
public class DeviceToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var name = value?.ToString()?.ToLowerInvariant() ?? "";
        if (name.Contains("iphone") || name.Contains("ipad"))    return ""; // Phone
        if (name.Contains("android") && name.Contains("手机"))    return ""; // Phone
        if (name.Contains("android"))                             return ""; // Tablet
        if (name.Contains("mac"))                                 return ""; // Mac
        if (name.Contains("linux"))                               return ""; // Terminal
        if (name.Contains("windows") || name.Contains("pc"))     return ""; // PC
        return ""; // Unknown / Globe
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// 根据设备名称返回图标背景色。
/// </summary>
public class DeviceToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush AppleBrush   = new(Color.FromRgb(0x55, 0x55, 0x55));
    private static readonly SolidColorBrush AndroidBrush = new(Color.FromRgb(0x3D, 0xD9, 0x4A));
    private static readonly SolidColorBrush WindowsBrush = new(Color.FromRgb(0x00, 0x78, 0xD4));
    private static readonly SolidColorBrush LinuxBrush   = new(Color.FromRgb(0xF0, 0xAD, 0x4E));
    private static readonly SolidColorBrush DefaultBrush = new(Color.FromRgb(0x9E, 0x9E, 0x9E));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var name = value?.ToString()?.ToLowerInvariant() ?? "";
        if (name.Contains("iphone") || name.Contains("ipad") || name.Contains("mac")) return AppleBrush;
        if (name.Contains("android"))  return AndroidBrush;
        if (name.Contains("windows"))  return WindowsBrush;
        if (name.Contains("linux"))    return LinuxBrush;
        return DefaultBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Bool → 运行状态指示器颜色
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            var invert = parameter?.ToString() == "Invert";
            return (b ^ invert) ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class RunningToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(0x2D, 0xB8, 0x4B));
    private static readonly SolidColorBrush RedBrush   = new(Color.FromRgb(0xE8, 0x48, 0x55));
    private static readonly SolidColorBrush GrayBrush  = new(Color.FromRgb(0x9E, 0x9E, 0x9E));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool running) return running ? GreenBrush : GrayBrush;
        return RedBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
