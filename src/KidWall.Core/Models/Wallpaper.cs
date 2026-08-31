using KidWall.Core.Models;

namespace KidWall.Core.Models;

/// <summary>一张壁纸。</summary>
public sealed class Wallpaper
{
    public Wallpaper(string id, string name, WallpaperCategory category, string fullPath, string thumbPath)
    {
        Id = id;
        Name = name;
        Category = category;
        FullPath = fullPath;
        ThumbPath = thumbPath;
    }

    /// <summary>唯一标识。</summary>
    public string Id { get; }

    /// <summary>展示名称。</summary>
    public string Name { get; }

    /// <summary>分类。</summary>
    public WallpaperCategory Category { get; }

    /// <summary>搜索用标签。</summary>
    public string Tags { get; set; } = string.Empty;

    /// <summary>完整图片文件路径（用于应用为桌面壁纸）。</summary>
    public string FullPath { get; }

    /// <summary>缩略图路径（用于网格展示）。</summary>
    public string ThumbPath { get; }

    /// <summary>是否动态壁纸（视频文件，列表显示封面，预览和桌面播放使用原视频）。</summary>
    public bool IsDynamic { get; set; }

    /// <summary>是否精选（推荐分类）。</summary>
    public bool IsRecommended { get; set; }

    /// <summary>是否来自本地用户文件夹。</summary>
    public bool IsFromLocal { get; set; }

    /// <summary>来源源名称（内置 / 本地）。</summary>
    public string SourceName { get; set; } = string.Empty;

    public bool MatchesKeyword(string keyword) =>
        string.IsNullOrWhiteSpace(keyword)
        || Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
        || Tags.Contains(keyword, StringComparison.OrdinalIgnoreCase);
}
