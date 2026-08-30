using KidWall.Core.Models;

namespace KidWall.Core.Services;

/// <summary>
/// 精选壁纸源：扫描仓库 res/ 目录（编译时链接输出到应用目录 res/）下的分类子目录，
/// 每张图片即一张壁纸。目录结构与分类映射：
///   res/cartoon/* → 卡通 · res/starry/* → 星空 · res/illustration/* → 插画
/// </summary>
public sealed class BuiltInWallpaperSource : IWallpaperSource
{
    private static readonly (string Directory, WallpaperCategory Category)[] CategoryDirectories =
    [
        ("cartoon", WallpaperCategory.Cartoon),
        ("starry", WallpaperCategory.Starry),
        ("illustration", WallpaperCategory.Illustration),
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

        foreach (var (directory, category) in CategoryDirectories)
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

                wallpapers.Add(new Wallpaper(
                    $"res:{file}",
                    Path.GetFileNameWithoutExtension(file),
                    category,
                    file,
                    file)
                {
                    Tags = $"{directory} 儿童",
                    SourceName = Name,
                });
            }
        }

        return Task.FromResult<IReadOnlyList<Wallpaper>>(wallpapers);
    }
}
