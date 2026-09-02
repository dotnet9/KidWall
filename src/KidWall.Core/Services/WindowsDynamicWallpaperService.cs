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
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private static readonly IntPtr HwndBottom = new(1);

    private const uint WsChild = 0x40000000;
    private const uint WsVisible = 0x10000000;
    private const uint WsPopup = 0x80000000;
    private const uint WsExNoActivate = 0x08000000;
    private const uint WsExToolWindow = 0x00000080;

    private const uint MonitorDefaultToNearest = 2;

    private readonly object _sync = new();
    private readonly List<WallpaperSurface> _surfaces = [];

    private LibVLC? _libVlc;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    /// <inheritdoc />
    public bool IsSupported => OperatingSystem.IsWindows();

    /// <inheritdoc />
    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _surfaces.Count != 0;
            }
        }
    }

    /// <inheritdoc />
    public bool Show(string videoPath)
    {
        if (!IsSupported || string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
        {
            return false;
        }

        lock (_sync)
        {
            var surfaces = new List<WallpaperSurface>();

            try
            {
                _libVlc ??= new LibVLC();

                var targets = FindDesktopWallpaperTargets();
                if (targets.Count == 0)
                {
                    return false;
                }

                var monitorCount = EnumerateMonitors().Count;
                if (monitorCount == 0 || targets.Count != monitorCount)
                {
                    return false;
                }

                foreach (var target in targets)
                {
                    surfaces.Add(CreateSurface(_libVlc, target, videoPath));
                }

                StopCore();
                _surfaces.AddRange(surfaces);
                return true;
            }
            catch
            {
                DisposeSurfaces(surfaces);
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
        return CreatePlayerWindow(parentWorkerW, bounds);
    }

    /// <summary>为指定显示器创建动态壁纸播放窗口。</summary>
    public static IntPtr CreatePlayerWindow(
        IntPtr parentWorkerW,
        int x,
        int y,
        int width,
        int height)
    {
        return CreatePlayerWindow(parentWorkerW, new ScreenBounds(x, y, width, height));
    }

    private static IntPtr CreatePlayerWindow(IntPtr parentWorkerW, ScreenBounds bounds)
    {
        if (parentWorkerW == IntPtr.Zero || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return IntPtr.Zero;
        }

        var window = CreateWindowEx(
            WsExNoActivate | WsExToolWindow,
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

        if (!ConfigureWallpaperWindow(window, parentWorkerW, bounds))
        {
            DestroyPlayerWindow(window);
            return IntPtr.Zero;
        }

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
        var bounds = GetVirtualScreenBounds();
        var worker = FindDesktopWorkerWindows()
            .FirstOrDefault(handle => WindowCovers(handle, bounds));
        return worker != IntPtr.Zero ? worker : FindWindow("Progman", null);
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

        return ConfigureWallpaperWindow(windowHandle, workerW, GetVirtualScreenBounds());
    }

    private void StopCore()
    {
        var surfaces = _surfaces.ToArray();
        _surfaces.Clear();
        DisposeSurfaces(surfaces);
    }

    private static bool ConfigureWallpaperWindow(IntPtr windowHandle, IntPtr workerW, ScreenBounds bounds)
    {
        // SetParent intentionally does not change WS_CHILD/WS_POPUP. Set the
        // child style first, then reparent and force the cached style to apply.
        var style = GetWindowLong(windowHandle, GwlStyle);
        var childStyle = (style | WsChild | WsVisible) & ~WsPopup;
        SetWindowLongPtr(windowHandle, GwlStyle, new IntPtr(childStyle));
        SetParent(windowHandle, workerW);
        if (GetParent(windowHandle) != workerW)
        {
            return false;
        }

        var parentX = 0;
        var parentY = 0;
        if (GetWindowRect(workerW, out var parentBounds))
        {
            parentX = bounds.X - parentBounds.Left;
            parentY = bounds.Y - parentBounds.Top;
        }

        return SetWindowPos(
            windowHandle,
            HwndBottom,
            parentX,
            parentY,
            bounds.Width,
            bounds.Height,
            SwpNoActivate | SwpFrameChanged | SwpShowWindow);
    }

    private static ScreenBounds GetVirtualScreenBounds() =>
        new(GetSystemMetrics(SmXVirtualScreen), GetSystemMetrics(SmYVirtualScreen), GetSystemMetrics(SmCxVirtualScreen), GetSystemMetrics(SmCyVirtualScreen));

    private static IReadOnlyList<WallpaperTarget> FindDesktopWallpaperTargets()
    {
        var monitors = EnumerateMonitors();
        var workers = FindDesktopWorkerWindows();
        var progman = FindWindow("Progman", null);
        if (monitors.Count == 0 || (workers.Count == 0 && progman == IntPtr.Zero))
        {
            return [];
        }

        var unusedWorkers = new HashSet<IntPtr>(workers);
        var targets = new List<WallpaperTarget>(monitors.Count);
        foreach (var monitor in monitors)
        {
            var worker = SelectWorkerForMonitor(monitor, workers, unusedWorkers);
            if (worker == IntPtr.Zero && WindowCovers(progman, monitor.Bounds))
            {
                // Some Windows shell configurations expose only tiny WorkerW
                // placeholders at each monitor's origin. Progman itself still
                // spans the virtual desktop and remains below SHELLDLL_DefView,
                // so it is a valid last-resort wallpaper parent.
                worker = progman;
            }

            if (worker == IntPtr.Zero)
            {
                continue;
            }

            unusedWorkers.Remove(worker);
            targets.Add(new WallpaperTarget(worker, monitor.Bounds));
        }

        return targets;
    }

    private static IntPtr SelectWorkerForMonitor(
        DisplayMonitor monitor,
        IReadOnlyList<IntPtr> workers,
        HashSet<IntPtr> unusedWorkers)
    {
        foreach (var worker in workers)
        {
            if (unusedWorkers.Contains(worker) &&
                MonitorFromWindow(worker, MonitorDefaultToNearest) == monitor.Handle &&
                WindowCovers(worker, monitor.Bounds))
            {
                return worker;
            }
        }

        foreach (var worker in workers)
        {
            if (unusedWorkers.Contains(worker) && WindowCovers(worker, monitor.Bounds))
            {
                return worker;
            }
        }

        // Explorer can expose one shared WorkerW for the whole virtual desktop.
        // Reuse it with monitor-relative child coordinates in that configuration.
        return workers.FirstOrDefault(worker => WindowCovers(worker, monitor.Bounds));
    }

    private static bool WindowCovers(IntPtr window, ScreenBounds bounds)
    {
        if (window == IntPtr.Zero || !GetWindowRect(window, out var rect))
        {
            return false;
        }

        return rect.Left <= bounds.X &&
            rect.Top <= bounds.Y &&
            rect.Right >= bounds.X + bounds.Width &&
            rect.Bottom >= bounds.Y + bounds.Height;
    }

    private static IReadOnlyList<DisplayMonitor> EnumerateMonitors()
    {
        var monitors = new List<DisplayMonitor>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (handle, _, _, _) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(handle, ref info))
            {
                monitors.Add(new DisplayMonitor(handle, ToScreenBounds(info.Monitor)));
            }

            return true;
        }, IntPtr.Zero);

        return monitors;
    }

    private static IReadOnlyList<IntPtr> FindDesktopWorkerWindows()
    {
        var progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
        {
            return [];
        }

        SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, SmtoNormal, 1000, out _);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var workers = new List<IntPtr>();
            AddWorkerWindows(progman, workers);

            // The documented shell topology is a WorkerW containing
            // SHELLDLL_DefView followed by a sibling WorkerW used as the
            // desktop background. Prefer that sibling over unrelated WorkerW
            // windows owned by other shell extensions.
            EnumWindows((topLevel, _) =>
            {
                if (FindWindowEx(topLevel, IntPtr.Zero, "SHELLDLL_DefView", null) == IntPtr.Zero)
                {
                    return true;
                }

                AddWorker(FindWindowEx(IntPtr.Zero, topLevel, "WorkerW", null), workers);
                return true;
            }, IntPtr.Zero);

            if (workers.Count == 0)
            {
                AddWorkerWindows(IntPtr.Zero, workers);
            }

            if (workers.Count != 0)
            {
                return workers;
            }

            Thread.Sleep(50);
        }

        return [];
    }

    private static void AddWorkerWindows(IntPtr parent, List<IntPtr> workers)
    {
        var candidate = IntPtr.Zero;
        while ((candidate = FindWindowEx(parent, candidate, "WorkerW", null)) != IntPtr.Zero)
        {
            if (FindWindowEx(candidate, IntPtr.Zero, "SHELLDLL_DefView", null) == IntPtr.Zero && !workers.Contains(candidate))
            {
                workers.Add(candidate);
            }
        }
    }

    private static void AddWorker(IntPtr worker, List<IntPtr> workers)
    {
        if (worker != IntPtr.Zero &&
            FindWindowEx(worker, IntPtr.Zero, "SHELLDLL_DefView", null) == IntPtr.Zero &&
            !workers.Contains(worker))
        {
            workers.Add(worker);
        }
    }

    private static void DisposeSurfaces(IEnumerable<WallpaperSurface> surfaces)
    {
        foreach (var surface in surfaces)
        {
            surface.Dispose();
        }
    }

    private static WallpaperSurface CreateSurface(LibVLC libVlc, WallpaperTarget target, string videoPath)
    {
        var playerWindow = CreatePlayerWindow(target.WorkerWindow, target.Bounds);
        if (playerWindow == IntPtr.Zero)
        {
            throw new InvalidOperationException("Could not create a wallpaper surface.");
        }

        MediaPlayer? player = null;
        Media? media = null;
        try
        {
            player = new MediaPlayer(libVlc)
            {
                Hwnd = playerWindow,
                Mute = true,
                Volume = 0,
            };

            // 按播放窗口的宽高比裁剪视频，保证视频铺满整块显示器，
            // 避免源视频比例与屏幕不一致时出现黑边（封面模式）。
            var bounds = target.Bounds;
            media = new Media(
                libVlc,
                new Uri(Path.GetFullPath(videoPath), UriKind.Absolute),
                $":input-repeat=65535",
                ":no-audio",
                $":crop={bounds.Width}:{bounds.Height}");

            if (!player.Play(media))
            {
                throw new InvalidOperationException("LibVLC rejected the wallpaper media.");
            }

            return new WallpaperSurface(playerWindow, player, media);
        }
        catch
        {
            try
            {
                player?.Stop();
            }
            catch
            {
            }

            player?.Dispose();
            media?.Dispose();
            DestroyPlayerWindow(playerWindow);
            throw;
        }
    }

    private sealed class WallpaperSurface(IntPtr windowHandle, MediaPlayer player, Media media) : IDisposable
    {
        public void Dispose()
        {
            try
            {
                player.Stop();
            }
            catch
            {
            }

            try
            {
                player.Dispose();
            }
            catch
            {
            }

            try
            {
                media.Dispose();
            }
            catch
            {
            }

            DestroyPlayerWindow(windowHandle);
        }
    }

    private readonly record struct WallpaperTarget(IntPtr WorkerWindow, ScreenBounds Bounds);

    private readonly record struct DisplayMonitor(IntPtr Handle, ScreenBounds Bounds);

    private readonly record struct ScreenBounds(int X, int Y, int Width, int Height);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    private static ScreenBounds ToScreenBounds(NativeRect rect) =>
        new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);

    private static long GetWindowLong(IntPtr hWnd, int nIndex)
    {
        if (IntPtr.Size == 8)
        {
            return GetWindowLongPtr64(hWnd, nIndex);
        }

        return GetWindowLong32(hWnd, nIndex);
    }

    private static void SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value)
    {
        if (IntPtr.Size == 8)
        {
            SetWindowLongPtr64(hWnd, nIndex, value);
        }
        else
        {
            SetWindowLong32(hWnd, nIndex, value);
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern long GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern long GetWindowLongPtr64(IntPtr hWnd, int nIndex);
}
