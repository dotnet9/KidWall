using KidWall.Core.Models;

namespace KidWall.Core.Services;

/// <summary>壁纸源：向应用提供壁纸集合。</summary>
public interface IWallpaperSource
{
    /// <summary>来源显示名。</summary>
    string Name { get; }

    /// <summary>加载（或生成）该源下的全部壁纸。</summary>
    Task<IReadOnlyList<Wallpaper>> LoadAsync(CancellationToken ct = default);
}
