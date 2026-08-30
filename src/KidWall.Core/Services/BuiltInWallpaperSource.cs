using KidWall.Core.Models;

namespace KidWall.Core.Services;

/// <summary>
/// 精选壁纸源：扫描仓库 res/ 目录（编译时链接输出到应用目录 res/）下的分类子目录，
/// 每张图片即一张壁纸。目录结构与分类映射：
///   res/cartoon/* → 卡通 · res/starry/* → 星空 · res/illustration/* → 插画 · res/dynamic/* → 动态
/// 动态分类的图片是动态壁纸的静态底图，由界面层叠加播放动效模拟。
/// </summary>
public sealed class BuiltInWallpaperSource : IWallpaperSource
{
    private static readonly (string Directory, WallpaperCategory Category, bool IsDynamic)[] CategoryDirectories =
    [
        ("cartoon", WallpaperCategory.Cartoon, false),
        ("starry", WallpaperCategory.Starry, false),
        ("illustration", WallpaperCategory.Illustration, false),
        ("dynamic", WallpaperCategory.Dynamic, true),
    ];

    /// <summary>精选壁纸：每分类前两张，供“推荐”分类使用。</summary>
    private static readonly HashSet<string> RecommendedFiles =
    [
        "cartoon/01-dino-balloons",
        "cartoon/02-unicorn-rainbow",
        "starry/01-moon-stars",
        "starry/02-rocket-space",
        "illustration/01-mushroom-forest",
        "illustration/02-underwater",
        "dynamic/01-aurora-sea",
        "dynamic/02-starfield",
    ];

    private static readonly string[] SupportedExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".webp"];

    private readonly string _resDirectory;

    public BuiltInWallpaperSource(string resDirectory)
    {
        _resDirectory = resDirectory;
    }

    public string Name => "精选";

    public Task<IReadOnlyList<Wallpaper>> LoadAsync(CancellationToken ct = default)
    {
        var wallpapers = new List<Wallpaper>();
        if (!Directory.Exists(_resDirectory))
        {
            return Task.FromResult<IReadOnlyList<Wallpaper>>(wallpapers);
        }

        foreach (var (directory, category, isDynamic) in CategoryDirectories)
        {
            var folder = Path.Combine(_resDirectory, directory);
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(folder).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                ct.ThrowIfCancellationRequested();
                if (!SupportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                {
                    continue;
                }

                // 统一使用正斜杠，保证跨平台与推荐集合匹配
                var relativeKey = Path.Combine(directory, Path.GetFileNameWithoutExtension(file)).Replace('\\', '/');
                wallpapers.Add(new Wallpaper(
                    $"res:{file}",
                    Path.GetFileNameWithoutExtension(file),
                    category,
                    file,
                    file)
                {
                    Tags = isDynamic ? $"{directory} 儿童 动态" : $"{directory} 儿童",
                    IsDynamic = isDynamic,
                    IsRecommended = RecommendedFiles.Contains(relativeKey),
                    SourceName = Name,
                });
            }
        }

        return Task.FromResult<IReadOnlyList<Wallpaper>>(wallpapers);
    }
}
