using System.Runtime.InteropServices;
using LibVLC = LibVLCSharp.Shared.LibVLC;
using Media = LibVLCSharp.Shared.Media;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace KidWall.Core.Services;

/// <summary>
/// Windows 动态壁纸宿主：把 LibVLC 视频窗口挂到桌面壁纸层（WorkerW）。
/// </summary>
public sealed class WindowsDynamicWallpaperService : IDynamicWallpaperService
{
    private const uint SmtoNormal = 0x0000;
    private const int GwlStyle = -16;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;
    private const uint SwpFrameChanged = 0x0020;

    private const uint SwpNoActivate = 0x0010;
    private static readonly IntPtr HwndBottom = new(1);

    private const uint WsChild = 0x40000000;
    private const uint WsVisible = 0x10000000;
    private const uint WsPopup = 0x80000000;

    private readonly object _sync = new();

    private LibVLC? _libVlc;
    private MediaPlayer? _player;
    private Media? _media;
    private IntPtr _playerWindow;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

    [DllImport("user32.dll")]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, long dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    /// <inheritdoc />
    public bool IsSupported => true;

    /// <inheritdoc />
    public bool IsRunning => _playerWindow != IntPtr.Zero;

    /// <inheritdoc />
    public bool Show(string videoPath)
    {
        if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
        {
            return false;
        }

        lock (_sync)
        {
            MediaPlayer? player = null;
            Media? media = null;
            IntPtr playerWindow = IntPtr.Zero;

            try
            {
                _libVlc ??= new LibVLC();

                var workerW = FindDesktopWorkerW();
                if (workerW == IntPtr.Zero)
                {
                    return false;
                }

                playerWindow = CreatePlayerWindow(workerW);
                if (playerWindow == IntPtr.Zero)
                {
                    return false;
                }

                player = new MediaPlayer(_libVlc)
                {
                    Hwnd = playerWindow
                };

                media = new Media(
                    _libVlc,
                    new Uri(Path.GetFullPath(videoPath), UriKind.Absolute),
                    ":input-repeat=65535");

                player.Play(media);

                StopCore();
                _playerWindow = playerWindow;
                _player = player;
                _media = media;
                return true;
            }
            catch
            {
                try
                {
                    media?.Dispose();
                }
                catch
                {
                }

                try
                {
                    player?.Dispose();
                }
                catch
                {
                }

                if (playerWindow != IntPtr.Zero)
                {
                    DestroyPlayerWindow(playerWindow);
                }

                return false;
            }
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        lock (_sync)
        {
            StopCore();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_sync)
        {
            StopCore();
            _libVlc?.Dispose();
            _libVlc = null;
        }
    }

    /// <summary>
    /// 在桌面壁纸层 WorkerW 下创建原生视频播放窗口（LibVLC 直接渲染到该 HWND）。
    /// 先创建顶层窗口，再 SetParent 挂到 WorkerW。
    /// </summary>
    public static IntPtr CreatePlayerWindow(IntPtr parentWorkerW)
    {
        if (parentWorkerW == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var bounds = GetVirtualScreenBounds();

        var window = CreateWindowEx(
            0,
            "Static",
            string.Empty,
            WsPopup | WsVisible,
            bounds.X, bounds.Y, bounds.Width, bounds.Height,
            IntPtr.Zero,
            IntPtr.Zero,
            GetModuleHandle(null),
            IntPtr.Zero);
        if (window == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        ConfigureWallpaperWindow(window, parentWorkerW, bounds);
        return window;
    }

    /// <summary>销毁动态壁纸播放窗口。</summary>
    public static void DestroyPlayerWindow(IntPtr windowHandle)
    {
        if (windowHandle != IntPtr.Zero)
        {
            DestroyWindow(windowHandle);
        }
    }

    /// <summary>查找桌面壁纸层 WorkerW 窗口句柄；失败返回 IntPtr.Zero。</summary>
    public static IntPtr FindDesktopWorkerW()
    {
        var progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        SendMessageTimeout(progman, 0x052C, new IntPtr(0xD), IntPtr.Zero, SmtoNormal, 1000, out _);

        IntPtr workerW = IntPtr.Zero;
        while ((workerW = FindWindowEx(IntPtr.Zero, workerW, "WorkerW", null)) != IntPtr.Zero)
        {
            var defView = FindWindowEx(workerW, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView == IntPtr.Zero)
            {
                return workerW;
            }
        }

        return IntPtr.Zero;
    }

    /// <summary>把播放窗口挂到桌面壁纸层，作为桌面背景层运行。</summary>
    public static bool AttachToDesktop(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        var workerW = FindDesktopWorkerW();
        if (workerW == IntPtr.Zero)
        {
            return false;
        }

        ConfigureWallpaperWindow(windowHandle, workerW, GetVirtualScreenBounds());
        return true;
    }

    private void StopCore()
    {
        try
        {
            _player?.Stop();
        }
        catch
        {
        }

        _player?.Dispose();
        _player = null;

        _media?.Dispose();
        _media = null;

        DestroyPlayerWindow(_playerWindow);
        _playerWindow = IntPtr.Zero;
    }

    private static void ConfigureWallpaperWindow(IntPtr windowHandle, IntPtr workerW, (int X, int Y, int Width, int Height) bounds)
    {
        SetParent(windowHandle, workerW);

        var style = GetWindowLong(windowHandle, GwlStyle);
        SetWindowLongPtr(windowHandle, GwlStyle, (style | WsChild | WsVisible) & ~WsPopup);

        SetWindowPos(windowHandle, HwndBottom, 0, 0, bounds.Width, bounds.Height, SwpNoActivate | SwpFrameChanged);
    }

    private static (int X, int Y, int Width, int Height) GetVirtualScreenBounds() =>
        (0, 0, GetSystemMetrics(SmCxVirtualScreen), GetSystemMetrics(SmCyVirtualScreen));

    private static long GetWindowLong(IntPtr hWnd, int nIndex)
    {
        if (IntPtr.Size == 8)
        {
            return GetWindowLongPtr64(hWnd, nIndex);
        }

        return GetWindowLong32(hWnd, nIndex);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern long GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern long GetWindowLongPtr64(IntPtr hWnd, int nIndex);
}
