namespace KidWall.Core.Models;

/// <summary>
/// 内置壁纸目录配置（res/wallpapers.json）。
/// 增删资源、改名、排序、推荐/隐藏标记都在配置文件中维护，无需改动代码。
/// </summary>
public sealed class WallpaperCatalog
{
    /// <summary>分类目录映射：目录名 → 分类。</summary>
    public List<CatalogCategory> Categories { get; set; } = [];

    /// <summary>需要从列表中隐藏的资源 key（目录/文件名，不含扩展名）。</summary>
    public List<string> Hidden { get; set; } = [];

    /// <summary>静态图片扩展名（用于识别非动态目录中的图片）。</summary>
    public List<string> ImageExtensions { get; set; } = [".jpg", ".jpeg", ".png", ".bmp", ".webp"];

    /// <summary>视频扩展名（用于识别动态目录中的视频）。</summary>
    public List<string> VideoExtensions { get; set; } = [".webm", ".mp4", ".ogv", ".mov"];

    /// <summary>条目元数据：key = “目录/文件名”（不含扩展名），value 为该资源的展示信息。</summary>
    public Dictionary<string, CatalogItem> Items { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>一个分类目录的映射配置。</summary>
public sealed class CatalogCategory
{
    /// <summary>资源子目录名（如 cartoon / dynamic）。</summary>
    public string Directory { get; set; } = string.Empty;

    /// <summary>对应 WallpaperCategory 枚举名（如 cartoon / dynamic）。</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>该目录是否按动态壁纸（视频）处理。</summary>
    public bool Dynamic { get; set; }
}

/// <summary>单个壁纸条目的展示元数据。</summary>
public sealed class CatalogItem
{
    /// <summary>展示名称；为空时回退为文件名。</summary>
    public string? Name { get; set; }

    /// <summary>列表排序权重；未配置的排最后。</summary>
    public int? Order { get; set; }

    /// <summary>是否进入“推荐”分类。</summary>
    public bool Recommended { get; set; }
}
