using System.Text.Json;
using KidWall.Core.Models;

namespace KidWall.Core.Services;

/// <summary>
/// 精选壁纸源：扫描仓库 res/ 目录（编译时链接输出到应用目录 res/）下的分类子目录。
/// 分类目录、条目显示名、排序、推荐/隐藏等元数据统一读取 res/wallpapers.json，
/// 增删资源只需放入对应目录并编辑配置文件，无需改动代码。
/// </summary>
public sealed class BuiltInWallpaperSource : IWallpaperSource
{
    /// <summary>内嵌的默认目录配置（外部 res/wallpapers.json 缺失时使用）。</summary>
    private const string EmbeddedCatalogName = "KidWall.Core.wallpapers.json";

    /// <summary>动态视频封面（16:9 渐变底图，避免视频封面缺失）。</summary>
    private static readonly GradientSpec[] CoverSpecs =
    [
        new("星空紫", "cover", WallpaperCategory.Dynamic,
            new RgbColor(46, 16, 101), new RgbColor(27, 20, 64), new RgbColor(255, 209, 102), StarStyle.Twinkle, Bubbles: false),
        new("深海蓝", "cover", WallpaperCategory.Dynamic,
            new RgbColor(30, 58, 138), new RgbColor(15, 23, 42), new RgbColor(79, 209, 255), StarStyle.Twinkle, Bubbles: false),
        new("极光青", "cover", WallpaperCategory.Dynamic,
            new RgbColor(76, 29, 149), new RgbColor(14, 116, 144), new RgbColor(109, 255, 196), StarStyle.Twinkle, Bubbles: false),
        new("莓果粉", "cover", WallpaperCategory.Dynamic,
            new RgbColor(255, 154, 213), new RgbColor(124, 58, 237), new RgbColor(255, 209, 102), StarStyle.Twinkle, Bubbles: true),
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly string _resDirectory;
    private readonly string _coverCacheDirectory;
    private readonly WallpaperCatalog _catalog;

    public BuiltInWallpaperSource(string resDirectory)
    {
        _resDirectory = resDirectory;
        _coverCacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KidWall",
            "Covers");
        _catalog = LoadCatalog(resDirectory);
    }

    public string Name => "精选";

    public Task<IReadOnlyList<Wallpaper>> LoadAsync(CancellationToken ct = default)
    {
        var wallpapers = new List<Wallpaper>();
        if (!Directory.Exists(_resDirectory))
        {
            return Task.FromResult<IReadOnlyList<Wallpaper>>(wallpapers);
        }

        foreach (var categoryConfig in _catalog.Categories)
        {
            var directory = categoryConfig.Directory;
            var folder = Path.Combine(_resDirectory, directory);
            if (!Directory.Exists(folder) || !TryMapCategory(categoryConfig.Category, out var category))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(folder).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                ct.ThrowIfCancellationRequested();
                var extension = Path.GetExtension(file).ToLowerInvariant();
                var relativeKey = Path.Combine(directory, Path.GetFileNameWithoutExtension(file)).Replace('\\', '/');
                if (_catalog.Hidden.Contains(relativeKey, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (categoryConfig.Dynamic)
                {
                    // 动态分类：视频即动态壁纸条目；封面图仅作展示辅助，不单独成为条目
                    if (!_catalog.VideoExtensions.Contains(extension))
                    {
                        continue;
                    }

                    var cover = EnsureCover(folder, Path.GetFileNameWithoutExtension(file));
                    wallpapers.Add(new Wallpaper($"res:{file}", GetDisplayName(relativeKey, file), category, file, cover)
                    {
                        Tags = "动态 儿童 视频",
                        IsDynamic = true,
                        IsRecommended = IsRecommended(relativeKey),
                        SourceName = Name,
                    });
                }
                else
                {
                    if (!_catalog.ImageExtensions.Contains(extension))
                    {
                        continue;
                    }

                    wallpapers.Add(new Wallpaper($"res:{file}", GetDisplayName(relativeKey, file), category, file, file)
                    {
                        Tags = $"{directory} 儿童",
                        IsRecommended = IsRecommended(relativeKey),
                        SourceName = Name,
                    });
                }
            }
        }

        return Task.FromResult<IReadOnlyList<Wallpaper>>(
            wallpapers
                .OrderBy(GetSortOrder)
                .ThenBy(w => w.FullPath, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    private static bool TryMapCategory(string value, out WallpaperCategory category) =>
        Enum.TryParse(value, ignoreCase: true, out category);

    private string GetDisplayName(string relativeKey, string path) =>
        _catalog.Items.TryGetValue(relativeKey, out var item) && !string.IsNullOrWhiteSpace(item.Name)
            ? item.Name!
            : Path.GetFileNameWithoutExtension(path);

    private bool IsRecommended(string relativeKey) =>
        _catalog.Items.TryGetValue(relativeKey, out var item) && item.Recommended;

    /// <summary>排序权重：优先按配置 order，未配置的条目排最后。</summary>
    private int GetSortOrder(Wallpaper wallpaper)
    {
        var directory = Path.GetFileName(Path.GetDirectoryName(wallpaper.FullPath));
        var relativeKey = $"{directory}/{Path.GetFileNameWithoutExtension(wallpaper.FullPath)}";
        return _catalog.Items.TryGetValue(relativeKey, out var item) && item.Order.HasValue
            ? item.Order.Value
            : int.MaxValue;
    }

    /// <summary>加载目录配置：优先读取 res/wallpapers.json，缺失/损坏时回退到内嵌默认。</summary>
    private static WallpaperCatalog LoadCatalog(string resDirectory)
    {
        var externalPath = Path.Combine(resDirectory, "wallpapers.json");
        if (File.Exists(externalPath))
        {
            try
            {
                return Deserialize(File.ReadAllText(externalPath));
            }
            catch (Exception)
            {
                // 外部配置损坏时回退内嵌默认，不让资源加载崩溃
            }
        }

        using var stream = typeof(BuiltInWallpaperSource).Assembly.GetManifestResourceStream(EmbeddedCatalogName);
        if (stream is not null)
        {
            using var reader = new StreamReader(stream);
            try
            {
                return Deserialize(reader.ReadToEnd());
            }
            catch (Exception)
            {
            }
        }

        // 最后一层兜底：空配置（不做任何映射），资源扫描自然返回空列表
        return new WallpaperCatalog();
    }

    private static WallpaperCatalog Deserialize(string json) =>
        JsonSerializer.Deserialize<WallpaperCatalog>(json, JsonOptions) ?? new WallpaperCatalog();

    /// <summary>查找视频同名封面（jpg/png/bmp），缺失时生成一张渐变封面（.preview.bmp）。</summary>
    private string EnsureCover(string folder, string nameWithoutExtension)
    {
        foreach (var extension in _catalog.ImageExtensions)
        {
            var candidate = Path.Combine(folder, nameWithoutExtension + extension);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var coverPath = Path.Combine(_coverCacheDirectory, Path.GetFileName(folder), nameWithoutExtension + ".preview.bmp");
        if (!File.Exists(coverPath))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(coverPath)!);
                var seed = StableHash(nameWithoutExtension);
                var spec = CoverSpecs[(seed & int.MaxValue) % CoverSpecs.Length];
                // The gallery only displays a 280x158 card. A 640x360 cover
                // keeps first launch fast while retaining enough detail for
                // the larger preview poster.
                File.WriteAllBytes(coverPath, GradientBitmapFactory.Generate(640, 360, spec, seed: seed));
            }
            catch (Exception)
            {
                // 缓存写入失败时退回原资源目录，界面层再做最后兜底
                return Path.Combine(folder, nameWithoutExtension + ".preview.bmp");
            }
        }

        return coverPath;
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = unchecked((int)2166136261);
            foreach (var character in value)
            {
                hash ^= character;
                hash *= 16777619;
            }

            return hash;
        }
    }
}
