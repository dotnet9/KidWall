using System.Runtime.InteropServices;
using Avalonia.Platform;
using LibVLCSharp.Avalonia;

namespace KidWall.App.Controls;

/// <summary>
/// LibVLC video host that keeps rendering enabled but lets the Avalonia card
/// underneath receive mouse input. Native child windows otherwise win hit
/// testing even when the Avalonia control is marked as not hit-testable.
/// </summary>
public sealed class PointerTransparentVideoView : VideoView
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnableWindow(IntPtr hWnd, bool enable);

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var handle = base.CreateNativeControlCore(parent);
        if (OperatingSystem.IsWindows() && handle.Handle != IntPtr.Zero)
        {
            // A disabled child is ignored when Windows resolves mouse input,
            // while LibVLC can continue painting frames into the HWND.
            EnableWindow(handle.Handle, false);
        }

        return handle;
    }
}
