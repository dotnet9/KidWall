using Avalonia.Controls;
using Avalonia.Platform.Storage;
using KidWall.App.Views;

namespace KidWall.App.Services;

public sealed class MainWindowService : IMainWindowService
{
    private readonly MainWindow _window;
    private AboutWindow? _aboutWindow;

    public MainWindowService(MainWindow window)
    {
        _window = window;
    }

    public void Minimize() => _window.WindowState = WindowState.Minimized;

    public void ToggleMaximize() =>
        _window.WindowState = _window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    public void CloseToTray() => _window.Close();

    public void ShowAbout()
    {
        if (_aboutWindow is not null)
        {
            _aboutWindow.Activate();
            return;
        }

        _aboutWindow = new AboutWindow { DataContext = _window.DataContext };
        _aboutWindow.Closed += (_, _) => _aboutWindow = null;
        _ = _aboutWindow.ShowDialog(_window);
    }

    public async Task<string?> PickLocalFolderAsync()
    {
        var folders = await _window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = CodeWF.Toolkit.Lang.I18nManager.Instance.GetResource(Localization.Host.SelectFolder),
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }
}
