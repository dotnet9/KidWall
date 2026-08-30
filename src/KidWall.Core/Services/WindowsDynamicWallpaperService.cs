using System.Runtime.InteropServices;

namespace KidWall.Core.Services;

/// <summary>
/// WorkerW 桌面壁纸层注入：把动态壁纸播放窗口挂到系统桌面壁纸层（桌面图标之下、壁纸之上）。
/// 经典方案：找到 Progman → 触发创建 WorkerW → 定位不含 SHELLDLL_DefView 的 WorkerW 即壁纸层。
/// </summary>
public static class WindowsDynamicWallpaperService
{
    private const uint SmtoNormal = 0x0000;
    private const int GwlExstyle = -20;
    private const long WsExTransparent = 0x00000020;
    private const long WsExLayered = 0x00080000;

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

    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;
    private static readonly IntPtr HwndBottom = new(1);

    private const uint WsChild = 0x40000000;
    private const uint WsVisible = 0x10000000;
    private const uint WsPopup = 0x80000000;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    /// <summary>
    /// 在桌面壁纸层 WorkerW 下创建原生视频播放窗口（LibVLC 直接渲染到该 HWND）。
    /// 先创建顶层窗口，再 SetParent 挂到 WorkerW（直接以 WorkerW 为父创建子窗口在此环境会失败）。
    /// </summary>
    public static IntPtr CreatePlayerWindow(IntPtr parentWorkerW, int width, int height)
    {
        if (parentWorkerW == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var window = CreateWindowEx(
            0,
            "Static",
            string.Empty,
            WsPopup | WsVisible,
            0, 0, width, height,
            IntPtr.Zero,
            IntPtr.Zero,
            GetModuleHandle(null),
            IntPtr.Zero);
        if (window == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        SetParent(window, parentWorkerW);
        SetWindowPos(window, HwndBottom, 0, 0, width, height, SwpNoActivate);
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

        // 通知系统创建 WorkerW 壁纸层
        SendMessageTimeout(progman, 0x052C, new IntPtr(0xD), IntPtr.Zero, SmtoNormal, 1000, out _);

        // 不含 SHELLDLL_DefView 的 WorkerW 即壁纸层
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

    /// <summary>把播放窗口挂到桌面壁纸层，并启用鼠标穿透（不拦截桌面点击）。</summary>
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

        SetParent(windowHandle, workerW);

        // 置于 z 序最底（WorkerW 壁纸层内），避免遮挡桌面图标与普通窗口
        SetWindowPos(windowHandle, HwndBottom, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);

        // 鼠标穿透 + 分层
        var style = GetWindowLong(windowHandle, (int)GwlExstyle);
        SetWindowLongPtr(windowHandle, (int)GwlExstyle, style | WsExTransparent | WsExLayered);
        return true;
    }

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
