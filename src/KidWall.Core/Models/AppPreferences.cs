namespace KidWall.Core.Models;

/// <summary>应用偏好设置。</summary>
public sealed class AppPreferences
{
    /// <summary>开机自启。</summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>定时自动切换壁纸。</summary>
    public bool AutoRotate { get; set; }

    /// <summary>自动切换间隔（分钟）。</summary>
    public int AutoRotateIntervalMinutes { get; set; } = 30;

    /// <summary>动态壁纸省电模式（预留）。</summary>
    public bool DynamicBatterySaver { get; set; } = true;

    /// <summary>屏蔽不适合内容（预留）。</summary>
    public bool ContentFilter { get; set; } = true;

    /// <summary>最近一次应用的壁纸 ID。</summary>
    public string LastAppliedWallpaperId { get; set; } = string.Empty;

    /// <summary>最近一次应用的壁纸路径。</summary>
    public string LastAppliedWallpaperPath { get; set; } = string.Empty;

    /// <summary>用户收藏的壁纸稳定标识，刷新图库或重启后恢复。</summary>
    public List<string> FavoriteWallpaperIds { get; set; } = [];

    /// <summary>用户导入的本地文件夹，重启后重新扫描。</summary>
    public List<string> LocalFolders { get; set; } = [];

    /// <summary>界面语言。</summary>
    public string Language { get; set; } = "zh-CN";

    /// <summary>主题模式，与配色独立保存。</summary>
    public string ThemeMode { get; set; } = "System";

    /// <summary>共享主题配色。</summary>
    public string Palette { get; set; } = "DotNet";
}
