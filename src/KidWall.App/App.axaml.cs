using Avalonia;
using Avalonia.Controls;
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

    private static LibVLCSharp.Shared.LibVLC? _dynamicLibVlc;
    private static LibVLCSharp.Shared.MediaPlayer? _dynamicPlayer;
    private static LibVLCSharp.Shared.Media? _dynamicMedia;
    private static IntPtr _dynamicPlayerWindow;

    /// <summary>应用动态壁纸：LibVLC 视频直接渲染到桌面壁纸层（WorkerW）的原生子窗口。</summary>
    public static void ShowDynamicWallpaper(string videoPath)
    {
        try
        {
            CloseDynamicWallpaper();

            _dynamicLibVlc ??= new LibVLCSharp.Shared.LibVLC();
            var workerW = WindowsDynamicWallpaperService.FindDesktopWorkerW();
            if (workerW == IntPtr.Zero)
            {
                return;
            }

            var width = 1920;
            var height = 1080;
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
                {
                    MainWindow: { } mainWindow
                } && mainWindow.Screens.Primary is { } screen)
            {
                width = screen.Bounds.Width;
                height = screen.Bounds.Height;
            }

            _dynamicPlayerWindow = WindowsDynamicWallpaperService.CreatePlayerWindow(workerW, width, height);
            if (_dynamicPlayerWindow == IntPtr.Zero)
            {
                return;
            }

            _dynamicPlayer = new LibVLCSharp.Shared.MediaPlayer(_dynamicLibVlc)
            {
                Hwnd = _dynamicPlayerWindow
            };

            // 媒体源需持有引用，局部释放会导致播放停止
            _dynamicMedia = new LibVLCSharp.Shared.Media(
                _dynamicLibVlc,
                new Uri(videoPath),
                ":input-repeat=65535");
            _dynamicPlayer.Play(_dynamicMedia);
        }
        catch (Exception)
        {
            CloseDynamicWallpaper();
        }
    }

    /// <summary>关闭动态壁纸播放层（恢复静态壁纸）。</summary>
    public static void CloseDynamicWallpaper()
    {
        _dynamicPlayer?.Stop();
        _dynamicPlayer?.Dispose();
        _dynamicPlayer = null;

        _dynamicMedia?.Dispose();
        _dynamicMedia = null;

        WindowsDynamicWallpaperService.DestroyPlayerWindow(_dynamicPlayerWindow);
        _dynamicPlayerWindow = IntPtr.Zero;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        RegisterLocalization();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 初始化 LibVLC（libvlc 原生库在输出目录 libvlc/win-x64/，供动态壁纸视频解码）
            try
            {
                var libVlcPath = Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64");
                if (Directory.Exists(libVlcPath))
                {
                    LibVLCSharp.Shared.Core.Initialize(libVlcPath);
                }
            }
            catch (Exception)
            {
                // libvlc 缺失时仅动态壁纸视频不可用，不影响其余功能
            }

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
