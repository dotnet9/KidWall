using Avalonia.Controls;
using Avalonia.Platform.Storage;
using KidWall.App.Views;

namespace KidWall.App.Services;

public sealed class MainWindowService : IMainWindowService
{
    private readonly MainWindow _window;

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

    public async Task<string?> PickLocalFolderAsync()
    {
        var folders = await _window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择壁纸文件夹",
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }
}
