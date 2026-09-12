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

    public static byte[] Render(LiquidGlassRenderer.Frame frame, WallpaperSnapshot wallpaper, byte[] glyphMask, double? refractionWidth = null)
    {
        var optics = frame.Theme.EffectiveLiquidGlass;
        var scale = frame.Scale;
        var width = frame.Width;
        var height = frame.Height;
        var sigma = (float)optics.Blur * scale / 8f;

        // Compute Euclidean Distance Field and Outward Normals for the glyph mask
        ComputeDistanceField(glyphMask, width, height, out var distField, out var nxField, out var nyField, out var maxStrokeDepth, out var avgStrokeDepth);

        // Adaptive optics scaling for compact 1x1 widgets and 1-grid strips
        var is1x1 = (frame.Columns == 1 && frame.Rows == 1) || (frame.Columns == 0 && Math.Min(width / scale, height / scale) <= 110f && Math.Max(width / scale, height / scale) <= 115f);
        var is1Strip = !is1x1 && ((frame.Columns == 1 || frame.Rows == 1) || (frame.Columns == 0 && Math.Min(width / scale, height / scale) <= 130f));

        var glyphEdgeScale = is1x1 ? 0.60f : (is1Strip ? 0.80f : 1.0f);
        var glyphShiftScale = is1x1 ? 0.70f : (is1Strip ? 0.85f : 1.0f);
        var glyphRimScale = is1x1 ? 0.80f : (is1Strip ? 0.90f : 1.0f);

        // Representative stroke half-thickness (radius) derived directly from glyph distance field:
        var estimatedRadius = Math.Max(avgStrokeDepth * 1.85f, maxStrokeDepth * 0.82f);
        var strokeRadius = Math.Clamp(estimatedRadius, 2.0f * scale, Math.Min(width, height) * 0.25f);

        // Refraction width calculation:
        // When explicitly specified by widget configuration (e.g. Frameless Clock RefractionWidth):
        // 0 means no refraction lens distortion (flat crystal-clear backdrop).
        // >0 scales up to stroke half-thickness.
        // When null, falls back to adaptive calculation based on global optics.EdgeWidth.
        float lensWidth;
        float lensShift;
        float dispStrength;

        if (refractionWidth.HasValue)
        {
            var rw = (float)Math.Max(0.0, refractionWidth.Value);
            if (rw <= 0.001f)
            {
                lensWidth = 0f;
                lensShift = 0f;
                dispStrength = 0f;
            }
            else
            {
                lensWidth = Math.Clamp(rw * scale * glyphEdgeScale, 0.5f * scale, strokeRadius * 0.46f);
                lensShift = Math.Clamp((float)(optics.Refraction / 100.0) * lensWidth * 1.85f * glyphShiftScale, 0.5f * scale, lensWidth * 2.5f);
                dispStrength = (float)(optics.Dispersion / 100.0);
            }
        }
        else
        {
            var edgeFrac = (float)Math.Clamp((optics.EdgeWidth / 24.0) * 0.38f * glyphEdgeScale, 0.18f, 0.46f);
            lensWidth = Math.Clamp(strokeRadius * edgeFrac, 1.2f * scale, strokeRadius * 0.46f);
            lensShift = Math.Clamp((float)(optics.Refraction / 100.0) * lensWidth * 1.85f * glyphShiftScale, 0.8f * scale, lensWidth * 2.5f);
            dispStrength = (float)(optics.Dispersion / 100.0);
        }

        var dyeWidth = Math.Max(Math.Clamp(strokeRadius * 0.32f, 2.5f * scale, 12f * scale), lensWidth);
        var rimLineWidth = Math.Clamp(Math.Max(lensWidth, strokeRadius * 0.15f) * 0.22f * glyphRimScale, 0.70f * scale, 2.4f * scale);

        var pad = (int)Math.Ceiling(Math.Max(sigma * 3f, Math.Max(lensWidth + 8f, lensShift * 1.3f + 16f)));
        var info = new SKImageInfo(width + 2 * pad, height + 2 * pad);
        using var backdrop = SKSurface.Create(info);
        var canvas = backdrop.Canvas;
        canvas.Clear(wallpaper.Background);

        SKBitmap? image = wallpaper.CachedBitmap;
        bool ownsBitmap = false;
        if (image == null && wallpaper.ImageBytes != null)
        {
            try
            {
                image = SKBitmap.Decode(wallpaper.ImageBytes);
                ownsBitmap = true;
            }
            catch { }
        }

        try
        {
            if (image != null)
            {
                using var filter = sigma > 0 ? SKImageFilter.CreateBlur(sigma, sigma, SKShaderTileMode.Clamp) : null;
                using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High, ImageFilter = filter };
                canvas.Save();
                canvas.Translate(pad, pad);
                LiquidGlassRenderer.DrawWallpaper(canvas, image, paint, frame, wallpaper);
                canvas.Restore();
            }
        }
        finally
        {
            if (ownsBitmap)
            {
                image?.Dispose();
            }
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

                // --- Intelligent Edge Dyeing Algorithm for Numeral Meniscus ---
                var u1 = dyeWidth > 0.001f ? Math.Clamp(depth / dyeWidth, 0f, 1f) : 1f;
                var bezelAura = (depth < dyeWidth && dyeWidth > 0.001f) ? 0.5f * (1f + MathF.Cos(MathF.PI * u1)) : 0f;

                float hlR = 255f, hlG = 255f, hlB = 255f;
                float dyeR = 255f, dyeG = 255f, dyeB = 255f;
                float dyeWeight = 0f;

                if (edgeTint > 0.001f && bezelAura > 0.001f)
                {
                    // Absolute chroma & physical luminance gating
                    var maxC = Math.Max(middle.Red, Math.Max(middle.Green, middle.Blue));
                    var minC = Math.Min(middle.Red, Math.Min(middle.Green, middle.Blue));
                    var chroma = (float)(maxC - minC);
                    var chromaWeight = SmoothStep(10f, 26f, chroma);
                    var lumaGate = SmoothStep(8f, 26f, luma);
                    var colorWeight = chromaWeight * lumaGate;

                    if (colorWeight > 0.001f)
                    {
                        middle.ToHsl(out var h, out var s, out var l);
                        var glowS = Math.Clamp(s * 2.5f + 30f * colorWeight, 30f, 100f);
                        var glowL = Math.Clamp(l * 0.20f + 48f, 44f, 62f);
                        var pureGlow = SKColor.FromHsl(h, glowS, glowL);
                        dyeR = pureGlow.Red;
                        dyeG = pureGlow.Green;
                        dyeB = pureGlow.Blue;
                        dyeWeight = colorWeight;
                    }
                    else if (!string.IsNullOrEmpty(frame.Theme.AccentColor) && SKColor.TryParse(frame.Theme.AccentColor, out var accent))
                    {
                        dyeR = accent.Red;
                        dyeG = accent.Green;
                        dyeB = accent.Blue;
                        dyeWeight = 0.85f;
                    }

                    if (dyeWeight > 0.001f)
                    {
                        // 1. Vibrant chromatic glaze on the outer meniscus edge
                        var glazeStrength = Math.Clamp(edgeTint * 1.5f, 0f, 1f);
                        var glazeMix = glazeStrength * bezelAura * 0.85f * dyeWeight;
                        r += (dyeR - r) * glazeMix;
                        g += (dyeG - g) * glazeMix;
                        b += (dyeB - b) * glazeMix;

                        // 2. Specular highlight is strongly tinted with the saturated dye color
                        var hlTint = Math.Clamp(MathF.Pow(edgeTint, 0.55f) * 1.35f * dyeWeight, 0f, 1f);
                        hlR = (1f - hlTint) * 255f + hlTint * dyeR;
                        hlG = (1f - hlTint) * 255f + hlTint * dyeG;
                        hlB = (1f - hlTint) * 255f + hlTint * dyeB;
                    }
                }

                // Meniscus highlight & crisp glass rim line
                var cosL = nx * lx + ny * ly;
                var directional = MathF.Max(0f, cosL);
                var rimEdge = 1f - SmoothStep(0f, rimLineWidth, depth);

                // Directional specular glint: only the light-facing edge catches direct specular shine!
                var rimLight = rimEdge * (0.12f + 0.88f * MathF.Pow(directional, 1.2f));

                // Direct rim dye: guarantees the outer stroke perimeter is visibly dyed, not white!
                if (dyeWeight > 0.001f && edgeTint > 0.001f)
                {
                    var rimDyeFactor = rimEdge * Math.Clamp(edgeTint * 1.4f, 0f, 1f) * dyeWeight * 0.75f;
                    r += (dyeR - r) * rimDyeFactor;
                    g += (dyeG - g) * rimDyeFactor;
                    b += (dyeB - b) * rimDyeFactor;
                }

                var meniscusLight = 0f;
                var spreadWidth = lensWidth * 1.6f;
                if (depth < spreadWidth && lensWidth > 0.001f)
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

                var totalLight = highlightFactor * (rimLight * 1.10f + meniscusLight * 0.65f + ambientLuster);
                // Cap total light mix at 0.78 so specular glint never totally blinds out the saturated dye color underneath
                var lightMix = Math.Clamp(totalLight, 0f, 0.78f);

                r += (hlR - r) * lightMix;
                g += (hlG - g) * lightMix;
                b += (hlB - b) * lightMix;

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


    /// <summary>
    /// Builds the glyph distance field and its outward normals.
    ///
    /// The mask is an anti-aliased coverage map (0..255), i.e. it already encodes where the
    /// outline crosses each boundary pixel. Thresholding it to 0/1 — as this used to do — snaps
    /// the contour to whole pixels, which biases the whole depth field by up to half a pixel
    /// (measured: 0.44 px mean error). Since the glass rim line is only ~0.6 px wide, that bias
    /// makes the rim's inner boundary crawl in half-pixel steps along the contour, which reads as
    /// broken / pixelated edges. Seeding the transform from the coverage value instead keeps the
    /// field sub-pixel accurate, so the rim traces a smooth outline.
    /// </summary>
    private static void ComputeDistanceField(byte[] mask, int width, int height,
        out float[] dist, out float[] nx, out float[] ny,
        out float maxStrokeDepth, out float avgStrokeDepth)
    {
        var size = width * height;
        dist = new float[size];
        nx = new float[size];
        ny = new float[size];

        // Exact Euclidean distance transform over unit-spaced seeds. A chamfer/pass-based
        // approximation was used before: it overestimates distances by up to ~6% depending on
        // the edge's angle, which is a 0.24 px bias at stroke-depths — visible on a 0.6 px rim.
        dist = ExactDistanceTransform(width, height, mask);

        // Calculate stroke depth statistics
        var maxD = 0f;
        var sumD = 0.0;
        var count = 0;
        for (var i = 0; i < size; i++)
        {
            var d = dist[i];
            if (d > 0f && !float.IsInfinity(d))
            {
                if (d > maxD) maxD = d;
                sumD += d;
                count++;
            }
        }
        maxStrokeDepth = (count > 0) ? maxD : 2f;
        avgStrokeDepth = (count > 0) ? (float)(sumD / count) : 1f;

        // Raw distance gradient
        var rawGx = new float[size];
        var rawGy = new float[size];
        for (var y = 1; y < height - 1; y++)
        {
            var row = y * width;
            for (var x = 1; x < width - 1; x++)
            {
                var idx = row + x;
                if (mask[idx] > 10)
                {
                    rawGx[idx] = dist[idx + 1] - dist[idx - 1];
                    rawGy[idx] = dist[idx + width] - dist[idx - width];
                }
            }
        }

        // Smooth continuous normal field: 7-tap separable filter over glyph mask
        // Eliminates corner medial-axis creases and triangular seams
        var tempGx = new float[size];
        var tempGy = new float[size];
        var weights = new[] { 1f, 3f, 6f, 8f, 6f, 3f, 1f }; // radius 3

        for (var y = 0; y < height; y++)
        {
            var row = y * width;
            for (var x = 0; x < width; x++)
            {
                var idx = row + x;
                if (mask[idx] <= 10) continue;
                float sx = 0f, sy = 0f, sw = 0f;
                for (int k = -3; k <= 3; k++)
                {
                    var px = Math.Clamp(x + k, 0, width - 1);
                    var pidx = row + px;
                    if (mask[pidx] > 10)
                    {
                        var w = weights[k + 3];
                        sx += rawGx[pidx] * w;
                        sy += rawGy[pidx] * w;
                        sw += w;
                    }
                }
                if (sw > 0f)
                {
                    tempGx[idx] = sx / sw;
                    tempGy[idx] = sy / sw;
                }
            }
        }

        for (var y = 0; y < height; y++)
        {
            var row = y * width;
            for (var x = 0; x < width; x++)
            {
                var idx = row + x;
                if (mask[idx] <= 10) continue;
                float sx = 0f, sy = 0f, sw = 0f;
                for (int k = -3; k <= 3; k++)
                {
                    var py = Math.Clamp(y + k, 0, height - 1);
                    var pidx = py * width + x;
                    if (mask[pidx] > 10)
                    {
                        var w = weights[k + 3];
                        sx += tempGx[pidx] * w;
                        sy += tempGy[pidx] * w;
                        sw += w;
                    }
                }
                if (sw > 0f)
                {
                    var gx = sx / sw;
                    var gy = sy / sw;
                    var len = MathF.Sqrt(gx * gx + gy * gy);
                    if (len > 0.001f)
                    {
                        nx[idx] = -gx / len;
                        ny[idx] = -gy / len;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Exact Euclidean distance transform (Felzenszwalb &amp; Huttenlocher) over the seeds defined
    /// by <see cref="SeedDepth"/>. Distances are interpolated linearly between neighbouring rows so
    /// seeds sitting at different heights blend instead of producing a stretched, blocky field.
    /// Pixels outside the shape end up at zero, which is the reference the depths are measured to.
    /// </summary>
    private static float[] ExactDistanceTransform(int width, int height, byte[] mask)
    {
        try
        {
            return ExactDistanceTransformCore(width, height, mask);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"EDT failed for {width}x{height} mask={mask?.Length}: {ex.Message}", ex);
        }
    }

    private static float[] ExactDistanceTransformCore(int width, int height, byte[] mask)
    {
        var size = width * height;
        if (mask == null || mask.Length < size)
        {
            // Defensive: a truncated mask would otherwise read past its end.
            System.Diagnostics.Debug.WriteLine(
                $"[GlyphLiquidGlass] mask length {mask?.Length ?? -1} < {width}x{height}");
        }

        // Pass 1: distance to the nearest seed *within the same column*.
        var rowDist = new float[size];
        for (var x = 0; x < width; x++)
        {
            var nearest = float.NaN;
            var nearestY = 0;
            for (var y = 0; y < height; y++)
            {
                var idx = y * width + x;
                if (idx >= mask.Length) { rowDist[idx] = float.PositiveInfinity; continue; }
                var seed = SeedDepth(mask[idx]);
                if (seed.HasValue)
                {
                    nearest = seed.Value;
                    nearestY = y;
                    rowDist[idx] = 0f;
                }
                else if (float.IsNaN(nearest))
                {
                    rowDist[idx] = float.PositiveInfinity;
                }
                else
                {
                    var dy = y - nearestY;
                    rowDist[idx] = dy * dy;
                }
            }
        }

        // Pass 2: along each row, build the lower envelope of the parabolas rooted at the columns
        // that still have a finite candidate, then read off the minimum.
        var result = new float[size];
        // +2 because the envelope can hold `width` parabolas and is probed one slot past the last.
        var v = new int[width + 2];
        var z = new float[width + 2];

        for (var y = 0; y < height; y++)
        {
            var row = y * width;
            var k = 0;
            var pushed = false;
            v[0] = -1;
            z[0] = float.NegativeInfinity;
            z[1] = float.PositiveInfinity;

            for (var q = 0; q < width; q++)
            {
                if (float.IsPositiveInfinity(rowDist[row + q])) continue;

                var s = Intersection(rowDist, row, q, v[k]);
                while (k >= 0 && s <= z[k])
                {
                    // v[k] must be read only after k >= 0 is known: reading it inside the call
                    // would index -1 and throw instead of ending the envelope walk.
                    k--;
                    if (k < 0) { s = float.NegativeInfinity; break; }
                    s = Intersection(rowDist, row, q, v[k]);
                }

                k++;
                v[k] = q;
                z[k] = s;
                z[k + 1] = float.PositiveInfinity;
                pushed = true;
            }

            if (!pushed)
            {
                // No seed anywhere in this row or in the rows above/below it.
                for (var x = 0; x < width; x++) result[row + x] = 0f;
                continue;
            }

            k = 0;
            for (var x = 0; x < width; x++)
            {
                while (z[k + 1] < x) k++;
                var dx = x - v[k];
                var d2 = dx * dx + rowDist[row + v[k]];
                var d = MathF.Sqrt(Math.Max(0f, d2));

                // a = where the outline crosses this pixel relative to its centre, so the depth
                // is the distance to the outline: d - 0.5, floored at 0.
                var depth = d - 0.5f;
                result[row + x] = depth > 0f ? depth : 0f;
            }
        }

        return result;
    }

    /// <summary>First column where the parabola rooted at <paramref name="q"/> becomes lower than
    /// the one rooted at <paramref name="u"/>.</summary>
    private static float Intersection(float[] rowDist, int row, int q, int u)
    {
        if (u < 0) return float.NegativeInfinity;
        var fq = rowDist[row + q];
        var fu = rowDist[row + u];
        return ((fq + (float)q * q) - (fu + (float)u * u)) / (2f * (q - u));
    }

    private static float? SeedDepth(byte coverage)
    {
        if (coverage == 255) return null;          // fully inside: grown by the transform
        if (coverage == 0) return 0f;              // fully outside: the zero reference
        if (coverage <= 5) return 0f;              // negligible sliver of coverage

        // a = where the outline crosses the pixel, as an offset from its centre.
        var a = TrackOffset(coverage / 255.0);

        // Convert to a depth: positive inside the shape, clamped at the outline itself.
        var depth = (0.5f - a) * 0.5f;
        return depth > 0f ? depth : 0f;
    }

    /// <summary>
    /// Exact inversion of the pixel/outline overlap: given the covered fraction <paramref name="c"/>
    /// of a pixel cut by a straight edge, returns where that edge crosses the pixel as an offset
    /// from the centre (negative = the centre is inside the shape).
    /// </summary>
    private static float TrackOffset(double c)
    {
        c = Math.Clamp(c, 0.0, 1.0);

        // Edge between the pixel centre and one side (a <= 0.5): covered area = a^2/2.
        var a = 0.5 - Math.Sqrt(c / 2.0);

        // Edge past the centre (a >= 0.5): covered area = 1/4 - (1-a)^2/2.
        if (a < 0.0) a = 1.0 - Math.Sqrt(Math.Max(0.0, (1.0 - c) * 2.0 - 0.5));

        return (float)(a - 0.5);
    }

    private static float Displacement(float depth, float lensWidth, float lensShift)
    {
        if (depth <= 0f || depth >= lensWidth || lensWidth <= 0.001f || lensShift <= 0.001f) return 0f;
        var t = depth / lensWidth;
        var shape = MathF.Sin(MathF.PI * t) * MathF.Pow(1f - t, 1.4f) / 0.45f;
        return lensShift * Math.Max(0f, shape);
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
        var ix = Math.Clamp((int)MathF.Floor(x), 0, w - 1);
        var iy = Math.Clamp((int)MathF.Floor(y), 0, h - 1);
        var x1 = Math.Min(ix + 1, w - 1);
        var y1 = Math.Min(iy + 1, h - 1);
        var a = pixels[iy * w + ix];
        var b = pixels[iy * w + x1];
        var c = pixels[y1 * w + ix];
        var d = pixels[y1 * w + x1];
        var fx = x - ix;
        var fy = y - iy;
        return new SKColor(
            Lerp(a.Red, b.Red, c.Red, d.Red),
            Lerp(a.Green, b.Green, c.Green, d.Green),
            Lerp(a.Blue, b.Blue, c.Blue, d.Blue));

        byte Lerp(byte p, byte q, byte r, byte s) =>
            (byte)Math.Clamp((int)MathF.Round((p + (q - p) * fx) * (1f - fy) + (r + (s - r) * fx) * fy), 0, 255);
    }
}
