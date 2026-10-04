using KidWall.Core.Models;

namespace KidWall.Core.Services;

/// <summary>
/// Linux 桌面壁纸服务占位实现：壁纸功能正在开发中。
/// 设置壁纸时抛出 <see cref="PlatformNotSupportedException"/>（带友好文案），
/// 由界面捕获后经状态条提示，不阻塞应用其余功能。
/// </summary>
public sealed class LinuxDesktopWallpaperService : IDesktopWallpaperService
{
    public bool SetWallpaper(string imagePath)
    {
        throw new PlatformNotSupportedException("当前平台 Linux 的壁纸功能正在开发中，敬请期待 🙏");
    }

    public string? GetCurrentWallpaper() => null;
}
