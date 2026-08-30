namespace KidWall.Core.Services;

/// <summary>非 Windows 平台的动态壁纸空实现。</summary>
public sealed class NoOpDynamicWallpaperService : IDynamicWallpaperService
{
    public bool IsSupported => false;

    public bool IsRunning => false;

    public bool Show(string videoPath) => false;

    public void Stop()
    {
    }

    public void Dispose()
    {
    }
}
