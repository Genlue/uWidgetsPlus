using SkiaSharp;

namespace probe;

/// <summary>Shared synthetic "wallpaper": fine 20 px grid + saturated blobs + text.
/// Identical in both probes so the two renderers can be compared pixel for pixel.</summary>
public static class Backdrop
{
    public const int W = 1200;
    public const int H = 800;
    public const int CardX = 220;
    public const int CardY = 190;

    public static SKBitmap Build(int w = W, int h = H)
    {
        var bmp = new SKBitmap(w, h);
        using var canvas = new SKCanvas(bmp);
        using (var gradient = SKShader.CreateLinearGradient(
                   new SKPoint(0, 0), new SKPoint(w, h),
                   new[] { new SKColor(18, 26, 58), new SKColor(196, 58, 122), new SKColor(240, 186, 84) },
                   new float[] { 0f, 0.55f, 1f }, SKShaderTileMode.Clamp))
        using (var paint = new SKPaint { Shader = gradient })
            canvas.DrawRect(0, 0, w, h, paint);

        var random = new Random(7);
        for (var i = 0; i < 18; i++)
        {
            using var paint = new SKPaint { Color = SKColor.FromHsl(random.Next(0, 360), 90, 55).WithAlpha(150), IsAntialias = true };
            canvas.DrawCircle(random.Next(0, w), random.Next(0, h), random.Next(40, 150), paint);
        }
        for (var i = 0; i < 6; i++)
        {
            using var paint = new SKPaint { Color = new SKColor(255, 255, 255, 46), IsAntialias = true };
            canvas.DrawRect(random.Next(0, w - 200), random.Next(0, h - 120), random.Next(80, 200), random.Next(60, 120), paint);
        }
        using (var grid = new SKPaint { Color = new SKColor(255, 255, 255, 70), StrokeWidth = 1, IsAntialias = false })
        {
            for (var x = 0; x <= w; x += 20) canvas.DrawLine(x, 0, x, h, grid);
            for (var y = 0; y <= h; y += 20) canvas.DrawLine(0, y, w, y, grid);
        }
        using (var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 58))
        using (var text = new SKPaint { Color = SKColors.White, IsAntialias = true })
        {
            canvas.DrawText("LIQUID GLASS 20px", 60, 110, font, text);
            canvas.DrawText("refraction · dispersion", 60, 700, font, text);
        }
        return bmp;
    }

    public static SKBitmap Crop(SKBitmap source, int x, int y, int size)
    {
        var result = new SKBitmap(size, size);
        using var canvas = new SKCanvas(result);
        canvas.DrawBitmap(source, new SKRect(x, y, x + size, y + size), new SKRect(0, 0, size, size));
        return result;
    }

    public static void Save(SKBitmap bitmap, string path)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        Console.WriteLine($"wrote {path}");
    }
}
