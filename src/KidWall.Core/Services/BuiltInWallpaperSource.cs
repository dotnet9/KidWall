using KidWall.Core.Models;

namespace KidWall.Core.Services;

/// <summary>
/// 精选壁纸源：扫描仓库 res/ 目录（编译时链接输出到应用目录 res/）下的分类子目录。
/// 目录结构与分类映射：
///   res/cartoon/* → 卡通 · res/starry/* → 星空 · res/illustration/* → 插画
///   res/dynamic/*.webm|mp4 → 动态壁纸（真实视频），自动生成封面供列表展示
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

    /// <summary>精选壁纸：供“推荐”分类使用。</summary>
    private static readonly HashSet<string> RecommendedFiles =
    [
        "cartoon/01-dino-balloons",
        "cartoon/02-unicorn-rainbow",
        "starry/01-moon-stars",
        "starry/02-rocket-space",
        "illustration/01-mushroom-forest",
        "illustration/02-underwater",
        "dynamic/01-big-buck-bunny",
        "dynamic/02-night-sky",
    ];

    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".webp"];
    private static readonly string[] VideoExtensions = [".webm", ".mp4", ".ogv", ".mov"];

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

    private readonly string _resDirectory;
    private readonly string _coverCacheDirectory;

    public BuiltInWallpaperSource(string resDirectory)
    {
        _resDirectory = resDirectory;
        _coverCacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KidWall",
            "Covers");
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
                var extension = Path.GetExtension(file).ToLowerInvariant();

                if (isDynamic)
                {
                    // 动态分类：视频即动态壁纸条目；封面图仅作展示辅助，不单独成为条目
                    if (!VideoExtensions.Contains(extension))
                    {
                        continue;
                    }

                    var cover = EnsureCover(folder, Path.GetFileNameWithoutExtension(file));
                    var relativeKey = Path.Combine(directory, Path.GetFileNameWithoutExtension(file)).Replace('\\', '/');
                    wallpapers.Add(new Wallpaper($"res:{file}", Path.GetFileNameWithoutExtension(file), category, file, cover)
                    {
                        Tags = "动态 儿童 视频",
                        IsDynamic = true,
                        IsRecommended = RecommendedFiles.Contains(relativeKey),
                        SourceName = Name,
                    });
                }
                else
                {
                    if (!ImageExtensions.Contains(extension))
                    {
                        continue;
                    }

                    var relativeKey = Path.Combine(directory, Path.GetFileNameWithoutExtension(file)).Replace('\\', '/');
                    wallpapers.Add(new Wallpaper($"res:{file}", Path.GetFileNameWithoutExtension(file), category, file, file)
                    {
                        Tags = $"{directory} 儿童",
                        IsRecommended = RecommendedFiles.Contains(relativeKey),
                        SourceName = Name,
                    });
                }
            }
        }

        return Task.FromResult<IReadOnlyList<Wallpaper>>(wallpapers);
    }

    /// <summary>查找视频同名封面（jpg/png/bmp），缺失时生成一张渐变封面（.preview.bmp）。</summary>
    private string EnsureCover(string folder, string nameWithoutExtension)
    {
        foreach (var extension in ImageExtensions)
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
