using KidWall.Core.Models;

namespace KidWall.Core.Services;

/// <summary>星星风格。</summary>
public enum StarStyle
{
    None,
    Twinkle,
}

/// <summary>一张代码生成的童趣渐变壁纸规格。</summary>
public sealed record GradientSpec(
    string Name,
    string Tags,
    WallpaperCategory Category,
    RgbColor Top,
    RgbColor Bottom,
    RgbColor Accent,
    StarStyle Stars,
    bool Bubbles);

/// <summary>
/// 零依赖的 24 位 BMP 生成器：对角渐变底 + 星星光点 + 童趣泡泡，
/// 首次启动时由 <see cref="BuiltInWallpaperSource"/> 用于生成本地壁纸文件。
/// </summary>
public static class GradientBitmapFactory
{
    /// <summary>生成指定尺寸的 24bpp BMP 字节。</summary>
    public static byte[] Generate(int width, int height, GradientSpec spec, int seed = 42)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        var rowPadding = (4 - (width * 3) % 4) % 4;
        var stride = width * 3 + rowPadding;
        var dataOffset = 54;
        var sizeImage = stride * height;
        var fileSize = dataOffset + sizeImage;

        var buffer = new byte[fileSize];

        // 文件头（BITMAPFILEHEADER）
        buffer[0] = (byte)'B';
        buffer[1] = (byte)'M';
        WriteInt32(buffer, 2, fileSize);
        WriteInt32(buffer, 10, dataOffset);

        // 信息头（BITMAPINFOHEADER）
        WriteInt32(buffer, 14, 40);
        WriteInt32(buffer, 18, width);
        WriteInt32(buffer, 22, height);
        WriteInt16(buffer, 26, 1);          // planes
        WriteInt16(buffer, 28, 24);         // bit count
        WriteInt32(buffer, 34, sizeImage);

        var rng = new Random(seed);
        var stars = spec.Stars == StarStyle.None ? 0 : width * height / 9000;
        var starPts = new (int X, int Y, int R)[stars];
        for (var i = 0; i < stars; i++)
        {
            starPts[i] = (rng.Next(width), rng.Next(height), rng.Next(1, 3));
        }

        var bubbles = new (int X, int Y, int R)[spec.Bubbles ? width / 220 : 0];
        var bw = rng.Next(20, 40);
        for (var i = 0; i < bubbles.Length; i++)
        {
            bubbles[i] = (rng.Next(bw, width - bw), (int)(height * 0.72) + rng.Next(0, height / 6), rng.Next(16, 40));
        }

        // 像素区：bottom-up（height > 0），行内 BGR
        for (var y = 0; y < height; y++)
        {
            var rowStart = dataOffset + y * stride;
            for (var x = 0; x < width; x++)
            {
                var t = x * 0.55 / width + y * 0.45 / height;
                var c = RgbColor.Lerp(spec.Top, spec.Bottom, t);

                // 星星光晕（加白）
                foreach (var (sx, sy, sr) in starPts)
                {
                    var dx = sx - x;
                    var dy = sy - y;
                    var d2 = dx * dx + dy * dy;
                    var glow = sr * 2.6;
                    if (d2 <= glow * glow)
                    {
                        var d = Math.Sqrt(d2);
                        var strength = d <= sr ? 1.0 : 1.0 - (d - sr) / (glow - sr);
                        c = Blend(c, new RgbColor(255, 255, 255), strength * 0.9);
                    }
                }

                // 底部泡泡（半透明点缀）
                foreach (var (bx, by, br) in bubbles)
                {
                    var dx = bx - x;
                    var dy = by - y;
                    var d2 = dx * dx + dy * dy;
                    if (d2 <= br * br)
                    {
                        c = Blend(c, spec.Accent, 0.30);
                    }
                }

                var i = rowStart + x * 3;
                buffer[i] = c.B;
                buffer[i + 1] = c.G;
                buffer[i + 2] = c.R;
            }
        }

        return buffer;
    }

    private static RgbColor Blend(RgbColor back, RgbColor front, double alpha)
    {
        alpha = Math.Clamp(alpha, 0, 1);
        return new RgbColor(
            (byte)(back.R * (1 - alpha) + front.R * alpha),
            (byte)(back.G * (1 - alpha) + front.G * alpha),
            (byte)(back.B * (1 - alpha) + front.B * alpha));
    }

    private static void WriteInt16(byte[] buffer, int offset, short value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private static void WriteInt32(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }
}
