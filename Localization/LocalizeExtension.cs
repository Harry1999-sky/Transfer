using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Threading;

namespace LanTransfer.Localization;

/// <summary>
/// XAML 本地化标记扩展。语言切换时自动更新绑定值。
/// 用法：{loc:Localize KeyName}
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public class LocalizeExtension : MarkupExtension, INotifyPropertyChanged
{
    private static readonly List<WeakReference<LocalizeExtension>> _instances = new();

    private WeakReference<DependencyObject>? _targetRef;
    private DependencyProperty? _targetProperty;

    public string Key { get; set; } = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 绑定源属性 — 语言变化时触发 PropertyChanged 让 WPF 刷新 UI。
    /// </summary>
    public string Value => LanguageManager.GetString(Key);

    public LocalizeExtension() { }

    public LocalizeExtension(string key) => Key = key;

    static LocalizeExtension()
    {
        LanguageManager.LanguageChanged += OnLanguageChanged;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var valueProvider = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
        if (valueProvider?.TargetObject is DependencyObject targetObj &&
            valueProvider.TargetProperty is DependencyProperty dp)
        {
            _targetRef = new WeakReference<DependencyObject>(targetObj);
            _targetProperty = dp;
            _instances.Add(new WeakReference<LocalizeExtension>(this));

            // 创建以自身为 Source 的 Binding，语言变化时通过 PropertyChanged 自动刷新
            var binding = new Binding(nameof(Value))
            {
                Source = this,
                Mode = BindingMode.OneWay
            };
            BindingOperations.SetBinding(targetObj, dp, binding);
            return Value;
        }
        return LanguageManager.GetString(Key);
    }

    private static void OnLanguageChanged()
    {
        // 清理已回收的实例
        _instances.RemoveAll(wr => !wr.TryGetTarget(out _));

        foreach (var weakRef in _instances)
        {
            if (weakRef.TryGetTarget(out var ext))
                ext.UpdateValue();
        }
    }

    private void UpdateValue()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
    }
}
