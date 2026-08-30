using KidWall.Core.Models;
using KidWall.Core.Services;
using Xunit;

namespace KidWall.Core.Tests;

public class GradientBitmapFactoryTests
{
    private static readonly GradientSpec Spec = new(
        "测试", "test", WallpaperCategory.Cartoon,
        new RgbColor(255, 0, 0), new RgbColor(0, 0, 255), new RgbColor(255, 255, 255),
        StarStyle.Twinkle, Bubbles: true);

    [Fact]
    public void Generate_ProducesValidBmpHeader()
    {
        var data = GradientBitmapFactory.Generate(320, 180, Spec);

        Assert.Equal((byte)'B', data[0]);
        Assert.Equal((byte)'M', data[1]);
        Assert.Equal(data.Length, BitConverter.ToInt32(data, 2)); // 文件大小
        Assert.Equal(54, BitConverter.ToInt32(data, 10));        // 像素偏移
        Assert.Equal(40, BitConverter.ToInt32(data, 14));        // 信息头大小
    }

    [Fact]
    public void Generate_MatchRequestedDimensions()
    {
        var data = GradientBitmapFactory.Generate(1280, 720, Spec);

        Assert.Equal(1280, BitConverter.ToInt32(data, 18));
        Assert.Equal(720, BitConverter.ToInt32(data, 22));
        Assert.Equal(24, BitConverter.ToInt16(data, 28));

        // 54 字节头 + 每行 1280*3 字节（无填充），共 720 行
        var expectedSize = 54 + 1280 * 3 * 720;
        Assert.Equal(expectedSize, data.Length);
    }

    [Fact]
    public void Generate_DifferentSeedsProduceDifferentPixels()
    {
        var a = GradientBitmapFactory.Generate(400, 225, Spec, seed: 1);
        var b = GradientBitmapFactory.Generate(400, 225, Spec, seed: 2);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Generate_RejectsInvalidSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GradientBitmapFactory.Generate(0, 10, Spec));
    }
}
