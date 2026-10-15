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
using CodeWF.Toolkit.Lang;
using CodeWF.Toolkit.Lang.Json;
using CodeWF.Avalonia.Controls.Themes;

namespace KidWall.App;

public partial class App : Avalonia.Application
{
    public static StudioThemeController ThemeController { get; private set; } = null!;
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
        ThemeController = new StudioThemeController(this);
        ActualThemeVariantChanged += (_, _) => RefreshThemeAliases();
        RefreshThemeAliases();
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

        // macOS 上 ~/Library/Application Support 可能被开发沙箱拦截，统一使用 ~/.local/share/KidWall
        var dataDirectory = OperatingSystem.IsMacOS()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "KidWall")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KidWall");
        var resDirectory = Path.Combine(AppContext.BaseDirectory, "res");
        var bundleResources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Resources", "res"));
        if (!Directory.Exists(resDirectory) && OperatingSystem.IsMacOS() && Directory.Exists(bundleResources))
            resDirectory = bundleResources;
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
        preferences.Language = string.Equals(preferences.Language, "en-US", StringComparison.OrdinalIgnoreCase) ? "en-US" : "zh-CN";
        I18nManager.Instance.Culture = new CultureInfo(preferences.Language);
        ApplyThirdPartyCulture(preferences.Language);
        WallpaperService = OperatingSystem.IsWindows()
            ? new WindowsDesktopWallpaperService()
            : OperatingSystem.IsMacOS()
                ? new MacOsDesktopWallpaperService()
                : new LinuxDesktopWallpaperService();
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
        (_mainWindow?.DataContext as IDisposable)?.Dispose();
        ThemeController.Dispose();
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
        var resourceFolder = Path.Combine(AppContext.BaseDirectory, "I18n");
        var bundleResources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Resources", "I18n"));
        if (!Directory.Exists(resourceFolder) && OperatingSystem.IsMacOS() && Directory.Exists(bundleResources))
            resourceFolder = bundleResources;
        var langPlugin = new JsonLangPlugin
        {
            ResourceFolder = resourceFolder
        };

        I18nManager.Instance.Register(langPlugin, new CultureInfo("zh-CN"), out _);
    }

    public static void ApplyThirdPartyCulture(string culture)
    {
        if (Current is not { } app) return;
        foreach (var semi in app.Styles.OfType<Semi.Avalonia.SemiTheme>()) semi.Locale = CultureInfo.GetCultureInfo(culture);
        foreach (var ursa in app.Styles.OfType<Ursa.Themes.Semi.UrsaSemiTheme>()) ursa.Locale = CultureInfo.GetCultureInfo(culture);
    }

    private void RefreshThemeAliases()
    {
        foreach (var (alias, token) in new[]
        {
            ("KidWallWindowBackground", "StudioBackground"), ("KidWallSurfaceBrush", "StudioSurface"),
            ("KidWallTitlebarBrush", "StudioSurface"), ("KidWallTextBrush", "StudioText"),
            ("KidWallTextSecondaryBrush", "StudioMuted"), ("KidWallTextTertiaryBrush", "StudioMuted"),
            ("KidWallBorderBrush", "StudioBorder"), ("KidWallAccentBrush", "StudioAccent"),
            ("KidWallAccentGradientBrush", "StudioAccent"), ("PrimaryButtonBrush", "StudioPrimary")
        })
            if (TryGetResource(token, ActualThemeVariant, out var value)) Resources[alias] = value;
    }
}
