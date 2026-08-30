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
            "cartoon/ignored.txt",
            "other/01-unknown.jpg");

        try
        {
            var source = new BuiltInWallpaperSource(root);
            var wallpapers = await source.LoadAsync();

            Assert.Equal(4, wallpapers.Count);

            var cartoon = wallpapers.Where(w => w.Category == WallpaperCategory.Cartoon).ToList();
            var starry = wallpapers.Where(w => w.Category == WallpaperCategory.Starry).ToList();
            var illustration = wallpapers.Where(w => w.Category == WallpaperCategory.Illustration).ToList();

            Assert.Equal(2, cartoon.Count);
            Assert.Single(starry);
            Assert.Single(illustration);
            Assert.All(wallpapers, w => Assert.True(File.Exists(w.FullPath)));
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
