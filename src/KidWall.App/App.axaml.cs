using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Input;
using KidWall.App.Services;
using KidWall.App.ViewModels;
using KidWall.App.Views;
using KidWall.Core.Services;
using Lang.Avalonia;
using Lang.Avalonia.Json;

namespace KidWall.App;

public partial class App : Avalonia.Application
{
    public static AppPreferencesStore PreferencesStore { get; private set; } = null!;

    public static IDesktopWallpaperService WallpaperService { get; private set; } = null!;

    private Mutex? _singleInstanceMutex;
    private MainWindow? _mainWindow;
    private IDynamicWallpaperService? _dynamicWallpaperService;
    private IClassicDesktopStyleApplicationLifetime? _desktopLifetime;

    public App()
    {
        DataContext = this;
        ShowMainWindowCommand = new RelayCommand(ShowMainWindow);
        TrayIconCommand = new RelayCommand(ShowMainWindow);
        ExitApplicationCommand = new RelayCommand(ExitApp);
    }

    public ICommand ShowMainWindowCommand { get; }

    public ICommand TrayIconCommand { get; }

    public ICommand ExitApplicationCommand { get; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        RegisterLocalization();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            base.OnFrameworkInitializationCompleted();
            return;
        }

        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        const string mutexName = "KidWall_SingleInstance_7a8f3e2d";
        bool createdNew;
        _singleInstanceMutex = new Mutex(true, mutexName, out createdNew);
        if (!createdNew)
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            desktop.Shutdown();
            return;
        }

        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KidWall");
        var resDirectory = Path.Combine(AppContext.BaseDirectory, "res");
        var libVlcDirectory = Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64");

        try
        {
            if (Directory.Exists(libVlcDirectory))
            {
                LibVLCSharp.Shared.Core.Initialize(libVlcDirectory);
            }
            else
            {
                LibVLCSharp.Shared.Core.Initialize();
            }
        }
        catch
        {
            // libvlc 初始化失败时，动态预览和动态壁纸不可用，但主界面仍可运行。
        }

        PreferencesStore = new AppPreferencesStore(dataDirectory);
        var preferences = PreferencesStore.Load();
        I18nManager.Instance.Culture = new CultureInfo(preferences.Language);
        WallpaperService = new WindowsDesktopWallpaperService();
        _dynamicWallpaperService = OperatingSystem.IsWindows()
            ? new WindowsDynamicWallpaperService()
            : new NoOpDynamicWallpaperService();

        _mainWindow = new MainWindow();
        var shellWindowService = new MainWindowService(_mainWindow);
        _mainWindow.DataContext = new MainViewModel(
            preferences,
            PreferencesStore,
            WallpaperService,
            _dynamicWallpaperService,
            shellWindowService,
            resDirectory);
        desktop.MainWindow = _mainWindow;
        _desktopLifetime = desktop;

        desktop.Exit += (_, _) => CleanupRuntimeState();

        bool silent = false;
        foreach (var arg in Environment.GetCommandLineArgs())
        {
            if (arg.Equals("--silent", StringComparison.OrdinalIgnoreCase))
            {
                silent = true;
                break;
            }
        }

        if (!silent)
        {
            _mainWindow.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void CleanupRuntimeState()
    {
        try
        {
            _dynamicWallpaperService?.Dispose();
        }
        catch
        {
        }
        _dynamicWallpaperService = null;

        try
        {
            _singleInstanceMutex?.ReleaseMutex();
        }
        catch
        {
        }
        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        if (!_mainWindow.IsVisible)
        {
            _mainWindow.Show();
        }

        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Activate();
        _mainWindow.Focus();
    }

    private void ExitApp()
    {
        if (_mainWindow is not null)
        {
            _mainWindow.AllowCloseToExit = true;
        }

        _desktopLifetime?.Shutdown();
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
