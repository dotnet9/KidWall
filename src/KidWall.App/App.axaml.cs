using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KidWall.App.ViewModels;
using KidWall.App.Views;
using KidWall.Core.Services;
using Lang.Avalonia;
using Lang.Avalonia.Json;
using System.Globalization;
using System.IO;

namespace KidWall.App;

public partial class App : Application
{
    public static AppPreferencesStore PreferencesStore { get; private set; } = null!;

    public static IDesktopWallpaperService WallpaperService { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        RegisterLocalization();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KidWall");
            var resDirectory = Path.Combine(AppContext.BaseDirectory, "res");

            PreferencesStore = new AppPreferencesStore(dataDirectory);
            var preferences = PreferencesStore.Load();
            I18nManager.Instance.Culture = new CultureInfo(preferences.Language);
            WallpaperService = new WindowsDesktopWallpaperService();

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(preferences, PreferencesStore, WallpaperService, resDirectory)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void RegisterLocalization()
    {
        var langPlugin = new JsonLangPlugin
        {
            ResourceFolder = Path.Combine(AppContext.BaseDirectory, "I18n")
        };

        I18nManager.Instance.Register(langPlugin, new CultureInfo("zh-CN"), out _);
    }
}
