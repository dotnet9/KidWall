using KidWall.Core.Models;
using KidWall.Core.Services;
using Xunit;

namespace KidWall.Core.Tests;

public class BuiltInWallpaperSourceTests
{
    private static string CreateTempResTree(params string[] files)
    {
        var root = Path.Combine(Path.GetTempPath(), "kidwall-tests", Guid.NewGuid().ToString("N"));
        foreach (var file in files)
        {
            var path = Path.Combine(root, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, [0x42, 0x4D, 0, 0, 0, 0]);
        }

        return root;
    }

    [Fact]
    public async Task LoadAsync_ScansCategoryDirectories()
    {
        var root = CreateTempResTree(
            "cartoon/01-kitten.jpg",
            "cartoon/02-puppy.jpg",
            "starry/01-night.png",
            "illustration/01-castle.jpg",
            "dynamic/01-aurora.webm",
            "cartoon/ignored.txt",
            "other/01-unknown.jpg");

        try
        {
            var source = new BuiltInWallpaperSource(root);
            var wallpapers = await source.LoadAsync();

            Assert.Equal(5, wallpapers.Count);

            var cartoon = wallpapers.Where(w => w.Category == WallpaperCategory.Cartoon).ToList();
            var starry = wallpapers.Where(w => w.Category == WallpaperCategory.Starry).ToList();
            var illustration = wallpapers.Where(w => w.Category == WallpaperCategory.Illustration).ToList();
            var dynamic = wallpapers.Single(w => w.Category == WallpaperCategory.Dynamic);

            Assert.Equal(2, cartoon.Count);
            Assert.Single(starry);
            Assert.Single(illustration);
            Assert.True(dynamic.IsDynamic);
            Assert.EndsWith(".webm", dynamic.FullPath, StringComparison.OrdinalIgnoreCase);
            // 视频自动生成封面
            Assert.True(File.Exists(dynamic.ThumbPath), "动态壁纸应生成封面图");
            Assert.All(wallpapers, w => Assert.True(File.Exists(w.FullPath)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_ReusesExistingDynamicCover()
    {
        var root = CreateTempResTree(
            "dynamic/01-aurora.webm",
            "dynamic/01-aurora.jpg");

        try
        {
            var source = new BuiltInWallpaperSource(root);
            var wallpapers = await source.LoadAsync();
            var dynamic = wallpapers.Single();

            Assert.EndsWith("01-aurora.jpg", dynamic.ThumbPath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_MarksRecommendedWallpapers()
    {
        var root = CreateTempResTree(
            "cartoon/01-dino-balloons.jpg",
            "cartoon/02-unicorn-rainbow.jpg",
            "cartoon/03-other.jpg",
            "starry/01-moon-stars.jpg",
            "dynamic/01-big-buck-bunny.webm");

        try
        {
            var source = new BuiltInWallpaperSource(root);
            var wallpapers = await source.LoadAsync();

            var recommended = wallpapers.Where(w => w.IsRecommended).Select(w => w.Name).ToList();
            Assert.Contains("01-dino-balloons", recommended);
            Assert.Contains("02-unicorn-rainbow", recommended);
            Assert.Contains("01-moon-stars", recommended);
            Assert.Contains("01-big-buck-bunny", recommended);
            Assert.DoesNotContain("03-other", recommended);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_MissingDirectory_ReturnsEmpty()
    {
        var root = Path.Combine(Path.GetTempPath(), "kidwall-tests", "missing", Guid.NewGuid().ToString("N"));
        var source = new BuiltInWallpaperSource(root);

        var wallpapers = await source.LoadAsync();

        Assert.Empty(wallpapers);
    }
}
