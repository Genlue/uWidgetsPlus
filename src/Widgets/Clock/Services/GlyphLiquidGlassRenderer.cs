using System;
using System.Threading.Tasks;
using SkiaSharp;
using uWidgets.Core.Models.Settings;
using uWidgets.Services;

namespace Clock.Services;

/// <summary>
/// Optical Liquid Glass renderer applied directly inside numeral glyph contours.
/// Calculates an Euclidean Distance Field (EDT) over the glyph mask to evaluate
/// per-pixel distance-to-stroke-boundary and outward surface normals, creating
/// real optical lens refraction, chromatic dispersion, 3D specular highlights,
/// and iOS glass rim lines along the individual number strokes.
/// </summary>
public static class GlyphLiquidGlassRenderer
{
    private const float LensDips = 10.0f;
    private const float RimLineDips = 1.2f;
    private const float Saturation = 1.22f;
    private const float HighlightReference = 65.0f;

    public static byte[] Render(LiquidGlassRenderer.Frame frame, WallpaperSnapshot wallpaper, byte[] glyphMask)
    {
        var optics = frame.Theme.EffectiveLiquidGlass;
        var scale = frame.Scale;
        var width = frame.Width;
        var height = frame.Height;
        var sigma = (float)optics.Blur * scale / 8f;

        // Confine edge refraction to a delicate, sharp rim (1.5dp - 4.5dp)
        // so numeral strokes stay crystal clear and legible without heavy warping
        var maxLensWidth = Math.Max(1.5f * scale, Math.Min(width, height) * 0.08f);
        var lensWidth = Math.Clamp((float)optics.EdgeWidth * scale * 0.25f, 1.5f * scale, Math.Min(4.5f * scale, maxLensWidth));
        var lensShift = (float)(optics.Refraction / 100.0) * LensDips * scale;
        var rimLineWidth = Math.Clamp(RimLineDips * scale, 0.75f, 2.2f * scale);
        var dispStrength = (float)(optics.Dispersion / 100.0);

        // Compute Euclidean Distance Field and Outward Normals for the glyph mask
        ComputeDistanceField(glyphMask, width, height, out var distField, out var nxField, out var nyField);

        var pad = (int)Math.Ceiling(Math.Max(sigma * 3f, Math.Max(lensWidth + 8f, lensShift * 1.3f + 16f)));
        var info = new SKImageInfo(width + 2 * pad, height + 2 * pad);
        using var backdrop = SKSurface.Create(info);
        var canvas = backdrop.Canvas;
        canvas.Clear(wallpaper.Background);

        using var image = wallpaper.ImageBytes == null ? null : SKBitmap.Decode(wallpaper.ImageBytes);
        if (image != null)
        {
            using var filter = sigma > 0 ? SKImageFilter.CreateBlur(sigma, sigma, SKShaderTileMode.Clamp) : null;
            using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High, ImageFilter = filter };
            canvas.Save();
            canvas.Translate(pad, pad);
            LiquidGlassRenderer.DrawWallpaper(canvas, image, paint, frame, wallpaper);
            canvas.Restore();
        }

        using var background = backdrop.Snapshot();
        var colorHex = frame.Dark ? frame.Theme.EffectiveSolidBackgroundDark : frame.Theme.EffectiveSolidBackgroundLight;
        if (!SKColor.TryParse(colorHex, out var coating)) coating = frame.Dark ? new SKColor(46, 46, 46) : SKColors.White;
        var opacity = double.IsFinite(frame.Theme.OpacityLevel) ? Math.Clamp(frame.Theme.OpacityLevel, 0, 1) : 0.18;
        var tint = (float)opacity;
        var edgeTint = (float)(optics.EdgeTint / 100.0);
        var highlightFactor = (float)(optics.Highlight / HighlightReference);

        var angle = optics.LightAngle * Math.PI / 180.0;
        var lx = (float)Math.Cos(angle);
        var ly = (float)Math.Sin(angle);
        var l3x = lx * 0.65f;
        var l3y = ly * 0.65f;
        var l3z = 0.76f;

        using var source = SKBitmap.FromImage(background);
        var sourcePixels = source.Pixels;
        var sourceW = source.Width;
        var sourceH = source.Height;

        var pixels = new SKColor[width * height];
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount / 2, 2, 8) };

        Parallel.For(0, height, parallel, y =>
        {
            var yCoord = y + 0.5f;
            var ambientLuster = MathF.Max(0f, 1f - yCoord / height) * 0.03f;
            var rowOffset = y * width;

            for (var x = 0; x < width; x++)
            {
                var idx = rowOffset + x;
                var maskVal = glyphMask[idx];
                if (maskVal < 10)
                {
                    pixels[idx] = SKColors.Empty;
                    continue;
                }

                var depth = distField[idx];
                var nx = nxField[idx];
                var ny = nyField[idx];

                // Lens displacement
                var shift = Displacement(depth, lensWidth, lensShift);
                var sx = x + pad - nx * shift;
                var sy = y + pad - ny * shift;

                // Chromatic dispersion
                var rimT = (lensWidth > 0f && depth < lensWidth) ? Math.Clamp(depth / lensWidth, 0f, 1f) : 1f;
                var rimFalloff = (1f - rimT) * (1f - rimT);
                var dispShape = MathF.Sin(MathF.PI * rimT) * rimFalloff / 0.35f;
                var dispScale = (shift * 0.07f + 0.95f * scale) * dispShape;
                var split = dispStrength * dispScale;

                var middle = SamplePixel(sourcePixels, sourceW, sourceH, sx, sy);
                var red = middle;
                var blue = middle;
                if (split > 0.002f)
                {
                    red = SamplePixel(sourcePixels, sourceW, sourceH, sx + nx * split, sy + ny * split);
                    blue = SamplePixel(sourcePixels, sourceW, sourceH, sx - nx * split, sy - ny * split);
                }

                var clarityRamp = (opacity >= 0.99) ? 0f : (1f - SmoothStep(0f, lensWidth * 1.5f, depth));
                var localTint = tint * (1f - 0.28f * clarityRamp);

                var luma = 0.2126f * red.Red + 0.7152f * middle.Green + 0.0722f * blue.Blue;
                var adapt = FrostMix(luma);
                var r = Channel(red.Red, coating.Red, luma, adapt, localTint);
                var g = Channel(middle.Green, coating.Green, luma, adapt, localTint);
                var b = Channel(blue.Blue, coating.Blue, luma, adapt, localTint);

                // Meniscus highlight & crisp glass rim line
                var cosL = nx * lx + ny * ly;
                var rimEdge = 1f - SmoothStep(0f, rimLineWidth, depth);
                var directional = MathF.Max(0f, cosL);
                var rimLight = rimEdge * (0.35f + 0.65f * MathF.Pow(directional, 0.85f));

                var meniscusLight = 0f;
                var spreadWidth = lensWidth * 1.8f;
                if (depth < spreadWidth)
                {
                    var t = Math.Clamp(depth / lensWidth, 0f, 1f);
                    var tilt = (depth < lensWidth) ? MathF.Pow(1f - t, 2.0f) : 0f;
                    var tnx = nx * tilt * 0.82f;
                    var tny = ny * tilt * 0.82f;
                    var tnz = MathF.Sqrt(Math.Max(0.01f, 1f - tnx * tnx - tny * tny));

                    var ndotl = Math.Max(0f, tnx * l3x + tny * l3y + tnz * l3z);
                    var specGlint = MathF.Pow(ndotl, 28);
                    var specGlow = MathF.Pow(ndotl, 8);
                    var fresnel = MathF.Pow(1f - tnz, 3) * 0.35f;

                    var bevelLight = ((0.70f * specGlint + 0.30f * specGlow) * MathF.Max(0f, cosL) * 0.90f + fresnel * 0.25f) * (1f - t) * (1f - t);
                    var innerT = depth / spreadWidth;
                    var innerRoll = 0.5f * (1f + MathF.Cos(MathF.PI * innerT));
                    var innerSheen = MathF.Pow(ndotl, 6) * MathF.Max(0f, cosL) * 0.08f * innerRoll;

                    meniscusLight = bevelLight + innerSheen;
                }

                var totalLight = highlightFactor * (rimLight * 1.35f + meniscusLight * 0.75f + ambientLuster);
                var lightMix = Math.Clamp(totalLight, 0f, 1f);

                r += (255f - r) * lightMix;
                g += (255f - g) * lightMix;
                b += (255f - b) * lightMix;

                // Anti-aliased alpha at character boundaries
                var alpha = maskVal;
                pixels[idx] = new SKColor(ClampByte(r), ClampByte(g), ClampByte(b), alpha);
            }
        });

        using var resultSurface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var resultBitmap = new SKBitmap();
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            resultBitmap.InstallPixels(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul),
                handle.AddrOfPinnedObject());
            resultSurface.Canvas.DrawBitmap(resultBitmap, 0, 0);
            using var imageSnapshot = resultSurface.Snapshot();
            using var encoded = imageSnapshot.Encode(SKEncodedImageFormat.Png, 95);
            return encoded.ToArray();
        }
        finally
        {
            handle.Free();
        }
    }

    private static void ComputeDistanceField(byte[] mask, int width, int height,
        out float[] dist, out float[] nx, out float[] ny)
    {
        var size = width * height;
        dist = new float[size];
        nx = new float[size];
        ny = new float[size];

        const float INF = 1e6f;
        for (var i = 0; i < size; i++)
        {
            dist[i] = (mask[i] > 128) ? INF : 0f;
        }

        // 2-pass Euclidean Distance Transform (forward pass)
        for (var y = 1; y < height; y++)
        {
            var row = y * width;
            for (var x = 1; x < width - 1; x++)
            {
                var idx = row + x;
                if (dist[idx] > 0f)
                {
                    var d1 = dist[idx - 1] + 1f;
                    var d2 = dist[idx - width] + 1f;
                    var d3 = dist[idx - width - 1] + 1.414f;
                    var d4 = dist[idx - width + 1] + 1.414f;
                    dist[idx] = Math.Min(dist[idx], Math.Min(Math.Min(d1, d2), Math.Min(d3, d4)));
                }
            }
        }

        // Backward pass
        for (var y = height - 2; y >= 0; y--)
        {
            var row = y * width;
            for (var x = width - 2; x >= 1; x--)
            {
                var idx = row + x;
                if (dist[idx] > 0f)
                {
                    var d1 = dist[idx + 1] + 1f;
                    var d2 = dist[idx + width] + 1f;
                    var d3 = dist[idx + width + 1] + 1.414f;
                    var d4 = dist[idx + width - 1] + 1.414f;
                    dist[idx] = Math.Min(dist[idx], Math.Min(Math.Min(d1, d2), Math.Min(d3, d4)));
                }
            }
        }

        // Surface normals from distance gradient
        for (var y = 1; y < height - 1; y++)
        {
            var row = y * width;
            for (var x = 1; x < width - 1; x++)
            {
                var idx = row + x;
                if (mask[idx] > 10)
                {
                    var gx = dist[idx + 1] - dist[idx - 1];
                    var gy = dist[idx + width] - dist[idx - width];
                    var len = MathF.Sqrt(gx * gx + gy * gy);
                    if (len > 0.001f)
                    {
                        nx[idx] = -gx / len;
                        ny[idx] = -gy / len;
                    }
                    else
                    {
                        nx[idx] = 0f;
                        ny[idx] = 0f;
                    }
                }
            }
        }
    }

    private static float Displacement(float depth, float lensWidth, float lensShift)
    {
        if (lensWidth <= 0f || depth >= lensWidth) return 0f;
        var t = Math.Clamp(depth / lensWidth, 0f, 1f);
        var rimProfile = (1f - t) * (1f - t);
        return lensShift * rimProfile;
    }

    private static float Channel(byte val, byte coat, float luma, float adapt, float localTint)
    {
        var c = luma + (val - luma) * Saturation;
        c += (255f - c) * adapt;
        return c * (1f - localTint) + coat * localTint;
    }

    private static float FrostMix(float luma)
    {
        var normalized = luma / 255f;
        var deficit = Math.Clamp(1f - normalized, 0f, 1f);
        return Math.Min(0.09f, 0.015f + 0.07f * deficit * deficit);
    }

    private static float SmoothStep(float e0, float e1, float x)
    {
        var t = Math.Clamp((x - e0) / (e1 - e0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static byte ClampByte(float val) => (byte)Math.Clamp((int)Math.Round(val), 0, 255);

    private static SKColor SamplePixel(SKColor[] pixels, int w, int h, float x, float y)
    {
        var ix = Math.Clamp((int)Math.Round(x), 0, w - 1);
        var iy = Math.Clamp((int)Math.Round(y), 0, h - 1);
        return pixels[iy * w + ix];
    }
}
