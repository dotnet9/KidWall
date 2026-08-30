namespace KidWall.Core.Services;

/// <summary>应用桌面壁纸的服务抽象。</summary>
public interface IDesktopWallpaperService
{
    /// <summary>将指定图片设置为桌面壁纸。</summary>
    bool SetWallpaper(string imagePath);
}
