using KidWall.Core.Models;

namespace KidWall.Core.Services;

/// <summary>扫描本地文件夹中的图片作为壁纸源。</summary>
public sealed class LocalWallpaperSource : IWallpaperSource
{
    private static readonly string[] SupportedExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".webp"];

    private readonly string _folder;

    public LocalWallpaperSource(string folder)
    {
        _folder = folder;
    }

    public string Name => "本地";

    public Task<IReadOnlyList<Wallpaper>> LoadAsync(CancellationToken ct = default)
    {
        var wallpapers = new List<Wallpaper>();
        if (!Directory.Exists(_folder))
        {
            return Task.FromResult<IReadOnlyList<Wallpaper>>(wallpapers);
        }

        var index = 0;
        foreach (var file in Directory.EnumerateFiles(_folder, "*.*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (!SupportedExtensions.Contains(ext) || Path.GetFileName(file).StartsWith("thumb_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            wallpapers.Add(new Wallpaper($"local:{file}", Path.GetFileNameWithoutExtension(file), WallpaperCategory.Local, file, file)
            {
                Tags = Path.GetFileNameWithoutExtension(file),
                IsFromLocal = true,
                SourceName = Name,
            });
            index++;
        }

        return Task.FromResult<IReadOnlyList<Wallpaper>>(wallpapers);
    }
}
