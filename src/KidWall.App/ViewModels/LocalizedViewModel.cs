using CommunityToolkit.Mvvm.ComponentModel;
using Lang.Avalonia;

namespace KidWall.App.ViewModels;

public abstract partial class LocalizedViewModel : ObservableObject, IDisposable
{
    private bool _disposed;

    protected LocalizedViewModel()
    {
        I18nManager.Instance.CultureChanged += OnCultureChanged;
    }

    protected abstract void RefreshLocalizedText();

    /// <summary>读取本地化文本（key 为 T4 生成的强类型常量，含 Localization 前缀）。</summary>
    protected string L(string key) => I18nManager.Instance.GetResource(key);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        I18nManager.Instance.CultureChanged -= OnCultureChanged;
    }

    private void OnCultureChanged(object? sender, EventArgs e) => RefreshLocalizedText();
}
