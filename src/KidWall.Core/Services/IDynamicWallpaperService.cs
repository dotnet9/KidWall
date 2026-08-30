namespace KidWall.Core.Services;

/// <summary>动态壁纸宿主抽象。</summary>
public interface IDynamicWallpaperService : IDisposable
{
    /// <summary>是否支持当前平台。</summary>
    bool IsSupported { get; }

    /// <summary>当前是否正在播放动态壁纸。</summary>
    bool IsRunning { get; }

    /// <summary>显示指定视频壁纸。</summary>
    bool Show(string videoPath);

    /// <summary>停止动态壁纸。</summary>
    void Stop();
}
