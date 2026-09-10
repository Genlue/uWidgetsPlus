using System;
using System.Threading.Tasks;
using SkiaSharp;
using uWidgets.Core.Models.Settings;

namespace uWidgets.Services;

/// <summary>
/// Renders a cached static material, never widget content.
///
/// This is a lens, not a blur panel — the defining difference between Apple's
/// Liquid Glass and plain frosted glassmorphism. The rounded-rectangle distance
/// field gives a continuous outward normal and an inward depth; the depth drives
/// three overlapping bands:
///
///   • the <b>lens ring</b>  — inward displacement over <c>lensW</c> px at the rim,
///     with a squared falloff, so the backdrop visibly bends and magnifies around
///     the edge while the centre stays clear;
///   • the <b>edge reflection</b> — a much wider, softer band that adds three times
///     the displacement, so content near the rim is dragged across and folds back
///     on itself (the inverted echo iOS shows at the top and bottom of a pill);
///   • the <b>edge guard</b> — the outermost few px taper the total displacement to
///     zero, which both calms sub-pixel noise and creates the fold, because the
///     displacement then rises faster than the pixel position.
///
/// On top: a crisp, lighting-independent rim line (the 1 px glass edge iOS draws
/// around every element), vibrancy (saturation lift) plus an adaptive frost that
/// lightens dark backdrops, a very faint prismatic split at the rim, a flat-pane
/// normal for the specular sheen, and a soft inner shadow on the side away from
/// the light. Parameters follow a replica measured against iOS 26 (see
/// <c>docs</c> / the 项目解构报告 §18).
/// </summary>
public static class LiquidGlassRenderer
{
    public record Frame(int Width, int Height, float Scale, float Radius,
        float DesktopX, float DesktopY, float DesktopWidth, float DesktopHeight,
        float ScreenX, float ScreenY, float ScreenWidth, float ScreenHeight,
        Theme Theme, bool Dark, bool SettingsSurface = false, float PixelScale = 1);

    /// <summary>Rim displacement in DIPs at refraction = 100%. Prominent optical lens magnification.</summary>
    private const float LensDips = 32f;

    /// <summary>Crisp rim-line width in DIPs. Modern iOS distinct 1.6 dp glass stroke.</summary>
    private const float RimLineDips = 1.6f;

    /// <summary>Vibrancy: how much the glass boosts the backdrop's chroma.</summary>
    private const float Saturation = 1.20f;

    /// <summary>Adaptive frost: floor and lift to keep dark backdrops legible without milky veil.</summary>
    private const float FrostFloor = 0.015f;
    private const float FrostLift = 0.07f;
    private const float FrostCap = 0.09f;

    /// <summary>Highlight slider value that reproduces the measured iOS material.</summary>
    private const double HighlightReference = 65.0;

    /// <summary>Render one background to PNG. Safe on worker threads.</summary>
    public static byte[] Render(Frame frame, WallpaperSnapshot wallpaper)
    {
        var optics = frame.Theme.EffectiveLiquidGlass;
        var scale = frame.Scale;
        var width = frame.Width;
        var height = frame.Height;
        var sigma = (float)optics.Blur * scale / 8f;
        var radius = Math.Clamp(frame.Radius * scale, 0f, Math.Min(width, height) / 2f);

        // In iOS, the lens is strictly an edge meniscus/bezel. The center of the glass
        // is 100% flat and crystal-clear (zero displacement, zero X-crease).
        var maxLensWidth = Math.Max(0.5f, Math.Min(width, height) * 0.45f);
        var minLensWidth = Math.Min(4f * scale, maxLensWidth);
        var lensWidth = Math.Clamp((float)optics.EdgeWidth * scale, minLensWidth, maxLensWidth);
        var lensShift = (float)(optics.Refraction / 100.0) * LensDips * scale;
        var rimLineWidth = MathF.Max(RimLineDips * scale, 0.75f);
        var dispStrength = (float)(optics.Dispersion / 100.0);

        var field = new BevelField(width, height, radius);
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
            DrawWallpaper(canvas, image, paint, frame, wallpaper);
            canvas.Restore();
        }

        using var background = backdrop.Snapshot();
        var colorHex = frame.Dark ? frame.Theme.EffectiveSolidBackgroundDark : frame.Theme.EffectiveSolidBackgroundLight;
        if (!SKColor.TryParse(colorHex, out var coating)) coating = frame.Dark ? new SKColor(46, 46, 46) : SKColors.White;
        var opacity = double.IsFinite(frame.Theme.OpacityLevel) ? Math.Clamp(frame.Theme.OpacityLevel, 0, 1) : 0.18;
        var tint = frame.SettingsSurface ? (float)Math.Max(0.82, opacity) : (float)opacity;
        var edgeTint = (float)(optics.EdgeTint / 100.0);
        var highlightFactor = (float)(optics.Highlight / HighlightReference);

        // Light direction: 225° means light comes from top-left (cos 225° = -0.707, sin 225° = -0.707)
        var angle = optics.LightAngle * Math.PI / 180.0;
        var lx = (float)Math.Cos(angle);
        var ly = (float)Math.Sin(angle);

        // 3D light vector for specular meniscus highlights
        var l3x = lx * 0.65f;
        var l3y = ly * 0.65f;
        var l3z = 0.76f;

        using var source = SKBitmap.FromImage(background);
        var sourcePixels = source.Pixels;
        var pixels = new SKColor[width * height];
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount / 2, 2, 8) };

        Parallel.For(0, height, parallel, y =>
        {
            var yCoord = y + 0.5f;
            // Subtle ambient vertical luster: top of glass catches ambient skylight (+2.5%)
            var ambientLuster = MathF.Max(0f, 1f - yCoord / height) * 0.025f;

            for (var x = 0; x < width; x++)
            {
                var xCoord = x + 0.5f;
                var bevel = field.Evaluate(xCoord, yCoord);
                var depth = bevel.Depth;

                // Displacement: prominent optical lens at the rim, 0 in center
                var shift = Displacement(depth, lensWidth, lensShift);
                var sx = x + pad - bevel.Nx * shift;
                var sy = y + pad - bevel.Ny * shift;

                // Chromatic dispersion (prismatic split):
                // Physically, dispersion is wavelength-dependent refraction (ΔD ∝ shift) along
                // the curved meniscus bezel. It peaks in the curved bezel region and tapers
                // smoothly to 0 at the outer border (no edge tearing) and inner flat center.
                var rimT = (lensWidth > 0f && depth < lensWidth) ? Math.Clamp(depth / lensWidth, 0f, 1f) : 1f;
                var rimFalloff = (1f - rimT) * (1f - rimT);
                var dispShape = MathF.Sin(MathF.PI * rimT) * rimFalloff / 0.35f;
                var dispScale = (shift * 0.07f + 0.95f * scale) * dispShape;
                var split = dispStrength * dispScale;
                var middle = Sample(sx, sy);
                var red = middle;
                var blue = middle;
                if (split > 0.002f)
                {
                    red = Sample(sx + bevel.Nx * split, sy + bevel.Ny * split);
                    blue = Sample(sx - bevel.Nx * split, sy - bevel.Ny * split);
                }

                // Translucency transition: the curved meniscus edge has higher crystal clarity,
                // smoothly transitioning into the soft frosted coating in the interior.
                // When opacity is 1.0 (or SettingsSurface), it remains 100% solid pure color.
                var clarityRamp = (opacity >= 0.99) ? 0f : (1f - SmoothStep(0f, lensWidth * 1.5f, depth));
                var localTint = tint * (1f - 0.28f * clarityRamp);

                // Vibrancy + adaptive frost
                var luma = 0.2126f * red.Red + 0.7152f * middle.Green + 0.0722f * blue.Blue;
                var adapt = FrostMix(luma);
                var r = Channel(red.Red, coating.Red);
                var g = Channel(middle.Green, coating.Green);
                var b = Channel(blue.Blue, coating.Blue);

                // --- HyperOS 4 / Modern iOS Intelligent Colored Edge Highlight & Organic Aura ---
                // Dual-scale organic aura:
                // 1. Concentrated chromatic glaze on the meniscus bezel (depth in [0, lensWidth])
                var u1 = Math.Clamp(depth / lensWidth, 0f, 1f);
                var bezelAura = (depth < lensWidth) ? 0.5f * (1f + MathF.Cos(MathF.PI * u1)) : 0f;

                // 2. Wide, soft ambient diffusion that gracefully sweeps deep into the glass interior (up to 2.5x lensWidth)
                var innerSpread = lensWidth * 2.5f;
                var u2 = Math.Clamp(depth / innerSpread, 0f, 1f);
                var ambientDiffusion = (depth < innerSpread) ? 0.5f * (1f + MathF.Cos(MathF.PI * u2)) : 0f;

                // Seamless blend: 65% bezel crest + 35% interior ambient diffusion
                var softAura = 0.65f * bezelAura + 0.35f * ambientDiffusion;

                float hlR = 255f, hlG = 255f, hlB = 255f;
                float rimGlow = 0f;
                if (edgeTint > 0.001f && softAura > 0.001f)
                {
                    middle.ToHsl(out var h, out var s, out var l);
                    // Smooth, continuous chroma weight: 0 for grayscale/neutral (s <= 2%), 1 for colorful (s >= 10%)
                    var chromaWeight = SmoothStep(2f, 10f, s);
                    var glowS = Math.Clamp(s * 2.2f + 25f * chromaWeight, 0f, 100f);
                    var glowL = Math.Clamp(l * 0.15f + 48f, 46f, 60f);
                    var pureGlow = SKColor.FromHsl(h, glowS, glowL);

                    // Interpolate smoothly between neutral light and saturated spectral color (C1 continuous, no jagged thresholds)
                    var glowR = (1f - chromaWeight) * 255f + chromaWeight * pureGlow.Red;
                    var glowG = (1f - chromaWeight) * 255f + chromaWeight * pureGlow.Green;
                    var glowB = (1f - chromaWeight) * 255f + chromaWeight * pureGlow.Blue;

                    // 1. Soft chromatic glaze on the outer glass (only glazes when there is real color)
                    var glazeMix = edgeTint * softAura * 0.35f * chromaWeight;
                    r += (glowR - r) * glazeMix;
                    g += (glowG - g) * glazeMix;
                    b += (glowB - b) * glazeMix;

                    // 2. Dynamic colored highlight tint (tapering from saturated color at rim to pure white inside)
                    var hlMix = MathF.Pow(edgeTint, 0.70f) * softAura * chromaWeight;
                    hlR = (1f - hlMix) * 255f + hlMix * pureGlow.Red;
                    hlG = (1f - hlMix) * 255f + hlMix * pureGlow.Green;
                    hlB = (1f - hlMix) * 255f + hlMix * pureGlow.Blue;
                    rimGlow = edgeTint * softAura * softAura * 0.35f;
                }

                // --- Modern iOS Luminous Glass Highlights (Zero Dark Shadows) ---
                // 1. Omnidirectional crisp glass rim stroke with strong directional light boost
                var cosL = bevel.Nx * lx + bevel.Ny * ly; // 1 when facing light, -1 when opposite
                var rimEdge = 1f - SmoothStep(0f, rimLineWidth, depth);
                var directional = MathF.Max(0f, cosL);
                var rimLight = rimEdge * (0.30f + 0.70f * MathF.Pow(directional, 0.85f));

                // 2. Meniscus curved reflection and continuous surface specular spread
                var meniscusLight = 0f;
                var spreadWidth = lensWidth * 1.8f;
                if (depth < spreadWidth)
                {
                    var t = Math.Clamp(depth / lensWidth, 0f, 1f);
                    var tilt = (depth < lensWidth) ? MathF.Pow(1f - t, 2.0f) : 0f;
                    var nx = bevel.Nx * tilt * 0.82f;
                    var ny = bevel.Ny * tilt * 0.82f;
                    var nz = MathF.Sqrt(Math.Max(0.01f, 1f - nx * nx - ny * ny));

                    // Light reflection
                    var ndotl = Math.Max(0f, nx * l3x + ny * l3y + nz * l3z);
                    var specGlint = PowInt(ndotl, 28);
                    var specGlow = PowInt(ndotl, 8);
                    var fresnel = PowInt(1f - nz, 3) * 0.35f;

                    // Bezel glint on the curved edge
                    var bevelLight = ((0.70f * specGlint + 0.30f * specGlow) * MathF.Max(0f, cosL) * 0.90f + fresnel * 0.25f) * (1f - t) * (1f - t);

                    // Soft inner specular sheen rolling off into the interior
                    var innerT = depth / spreadWidth;
                    var innerRoll = 0.5f * (1f + MathF.Cos(MathF.PI * innerT));
                    var innerSheen = PowInt(ndotl, 6) * MathF.Max(0f, cosL) * 0.08f * innerRoll;

                    meniscusLight = bevelLight + innerSheen;
                }

                // 3. Combine highlights (Notice: ZERO depthShadow! Modern iOS is airy, clean, and shadowless)
                var totalLight = highlightFactor * (rimLight * 1.25f + meniscusLight * 0.70f + ambientLuster) + rimGlow;
                var lightMix = Math.Clamp(totalLight, 0f, 1f);

                // 4. Screen / Luminous Optical Blending with smart colored highlight
                r += (hlR - r) * lightMix;
                g += (hlG - g) * lightMix;
                b += (hlB - b) * lightMix;

                pixels[y * width + x] = new SKColor(Clamp(r), Clamp(g), Clamp(b));

                float Channel(byte value, byte coat)
                {
                    var c = luma + (value - luma) * Saturation;
                    c += (255f - c) * adapt;
                    return c * (1 - localTint) + coat * localTint;
                }
            }
        });

        using var refracted = new SKBitmap(width, height);
        refracted.Pixels = pixels;
        using var shader = refracted.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);
        using var output = SKSurface.Create(new SKImageInfo(width, height));
        output.Canvas.Clear(SKColors.Transparent);
        using var glassPaint = new SKPaint { Shader = shader, IsAntialias = true };
        output.Canvas.DrawRoundRect(new SKRect(0, 0, width, height), radius, radius, glassPaint);
        using var result = output.Snapshot();
        using var data = result.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();

        SKColor Sample(float x, float y)
        {
            x = Math.Clamp(x, 0, info.Width - 1);
            y = Math.Clamp(y, 0, info.Height - 1);
            var ix = (int)x; var iy = (int)y;
            var x1 = Math.Min(ix + 1, info.Width - 1); var y1 = Math.Min(iy + 1, info.Height - 1);
            var a = sourcePixels[iy * info.Width + ix]; var b = sourcePixels[iy * info.Width + x1];
            var c = sourcePixels[y1 * info.Width + ix]; var d = sourcePixels[y1 * info.Width + x1];
            var fx = x - ix; var fy = y - iy;
            return new SKColor(Lerp(a.Red, b.Red, c.Red, d.Red), Lerp(a.Green, b.Green, c.Green, d.Green), Lerp(a.Blue, b.Blue, c.Blue, d.Blue));
            byte Lerp(byte p, byte q, byte r, byte s) => (byte)Math.Clamp(MathF.Round((p + (q - p) * fx) * (1 - fy) + (r + (s - r) * fx) * fy), 0f, 255f);
        }
    }

    /// <summary>
    /// Lens displacement (px) at inward depth from the border.
    /// Confined strictly to the bezel meniscus [0, lensWidth].
    /// The profile starts at 0 at the outer border (no boundary tearing),
    /// reaches its peak in the outer third of the bezel (optical meniscus refraction),
    /// and tapers smoothly to 0 at lensWidth with C1 continuity.
    /// For depth >= lensWidth (the entire central area), displacement is IDENTICALLY 0.
    /// </summary>
    public static float Displacement(float depth, float lensWidth, float lensShift, float reflectWidth = 0f, float guardWidth = 0f)
    {
        if (depth <= 0f || depth >= lensWidth || lensWidth <= 0f) return 0f;
        var t = depth / lensWidth;
        var shape = MathF.Sin(MathF.PI * t) * MathF.Pow(1f - t, 1.4f) / 0.45f;
        return lensShift * Math.Max(0f, shape);
    }

    /// <summary>Adaptive frost mix (0-1) for a backdrop luminance 0-255.</summary>
    public static float FrostMix(float luma) =>
        Math.Clamp(FrostFloor + (1f - luma / 255f) * FrostLift, 0f, FrostCap);

    private static byte Clamp(float value) => (byte)Math.Clamp(MathF.Round(value), 0f, 255f);

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        if (edge1 <= edge0) return value >= edge1 ? 1f : 0f;
        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float PowInt(float value, int exponent)
    {
        var result = 1f;
        var factor = value;
        for (var e = exponent; e > 0; e >>= 1)
        {
            if ((e & 1) != 0) result *= factor;
            factor *= factor;
        }
        return result;
    }

    /// <summary>
    /// Smooth, continuous 2D distance and normal field for the rounded rectangle.
    /// Purely radial in corner quadrants, purely orthogonal on straight edges,
    /// seamless C1 continuity at tangent points, with NO medial axis crease.
    /// </summary>
    public readonly struct BevelField
    {
        public readonly float HalfWidth, HalfHeight, InnerWidth, InnerHeight, Radius;

        public BevelField(int width, int height, float radius)
        {
            HalfWidth = width / 2f;
            HalfHeight = height / 2f;
            Radius = Math.Clamp(radius, 0f, Math.Min(HalfWidth, HalfHeight));
            InnerWidth = HalfWidth - Radius;
            InnerHeight = HalfHeight - Radius;
        }

        public Bevel Evaluate(float x, float y)
        {
            var vx = x - HalfWidth;
            var vy = y - HalfHeight;
            var sx = vx >= 0 ? 1f : -1f;
            var sy = vy >= 0 ? 1f : -1f;
            var ax = MathF.Abs(vx);
            var ay = MathF.Abs(vy);
            var qx = ax - InnerWidth;
            var qy = ay - InnerHeight;

            // Region 1: In the outer corner quadrant
            if (qx > 0f && qy > 0f)
            {
                var len = MathF.Sqrt(qx * qx + qy * qy);
                if (len > 1e-5f)
                {
                    return new Bevel(qx / len * sx, qy / len * sy, Radius - len);
                }
                return new Bevel(0.7071f * sx, 0.7071f * sy, Radius);
            }

            // Region 2: Horizontal straight edge (top or bottom)
            if (qx <= 0f && qy > 0f)
            {
                return new Bevel(0f, sy, HalfHeight - ay);
            }

            // Region 3: Vertical straight edge (left or right)
            if (qx > 0f && qy <= 0f)
            {
                return new Bevel(sx, 0f, HalfWidth - ax);
            }

            // Region 4: Interior region (ax <= InnerWidth, ay <= InnerHeight)
            var distV = HalfWidth - ax;
            var distH = HalfHeight - ay;
            var depth = MathF.Min(distV, distH);

            var u = -qx;
            var v = -qy;
            var angle = MathF.Atan2(u + 1e-4f, v + 1e-4f);
            var nx = MathF.Cos(angle) * sx;
            var ny = MathF.Sin(angle) * sy;

            return new Bevel(nx, ny, depth);
        }
    }

    /// <summary>Outward unit normal and inward depth at one point of the surface.</summary>
    public readonly struct Bevel
    {
        public readonly float Nx, Ny, Depth;

        public Bevel(float nx, float ny, float depth)
        {
            Nx = nx; Ny = ny; Depth = depth;
        }
    }

    public static void DrawWallpaper(SKCanvas canvas, SKBitmap image, SKPaint paint,
        Frame frame, WallpaperSnapshot wallpaper)
    {
        // Manual wallpaper alignment (DIPs → render px): the user calibrates the
        // sampled position against the real desktop when the display layer's own
        // layout cannot be trusted (taskbar replacements, wallpaper engines).
        var optics2 = frame.Theme.EffectiveLiquidGlass;
        var offX = (float)(optics2.WallpaperOffsetX * frame.Scale);
        var offY = (float)(optics2.WallpaperOffsetY * frame.Scale);

        // Live desktop capture: the bitmap IS the virtual desktop at physical
        // pixels (captured on Progman), so placement is identity — no style math,
        // no registry — the sampled crop is exactly what is displayed behind the
        // widget even when the display layout was changed by taskbar replacements
        // or wallpaper engines.
        if (wallpaper.LiveCapture)
        {
            var rect = SKRect.Create(-frame.DesktopX - offX, -frame.DesktopY - offY,
                image.Width * frame.PixelScale, image.Height * frame.PixelScale);
            canvas.DrawBitmap(image, rect, paint);
            return;
        }

        var style = wallpaper.Style;
        var tile = wallpaper.Tile;
        // Desktop and screen coordinates are already expressed in render pixels.
        // Preserve the global origin for negative monitor positions and margins.
        var x = frame.ScreenX - frame.DesktopX;
        var y = frame.ScreenY - frame.DesktopY;
        var w = frame.ScreenWidth;
        var h = frame.ScreenHeight;
        if (style == "22") // Span across the virtual desktop.
        {
            x = -frame.DesktopX;
            y = -frame.DesktopY;
            w = frame.DesktopWidth;
            h = frame.DesktopHeight;
        }
        if (tile)
        {
            var matrix = SKMatrix.CreateScale(frame.PixelScale, frame.PixelScale);
            matrix.TransX = x - offX;
            matrix.TransY = y - offY;
            using var tiled = image.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat,
                matrix);
            using var tiledPaint = new SKPaint { Shader = tiled, ImageFilter = paint.ImageFilter };
            canvas.DrawPaint(tiledPaint);
            return;
        }
        float drawWidth, drawHeight;
        if (style == "2") { drawWidth = w; drawHeight = h; } // Stretch
        else if (style == "0") { drawWidth = image.Width * frame.PixelScale; drawHeight = image.Height * frame.PixelScale; } // Center
        else
        {
            var ratio = style == "6" ? Math.Min(w / image.Width, h / image.Height) : Math.Max(w / image.Width, h / image.Height);
            drawWidth = image.Width * ratio;
            drawHeight = image.Height * ratio;
        }
        canvas.DrawBitmap(image, SKRect.Create(x + (w - drawWidth) / 2 - offX, y + (h - drawHeight) / 2 - offY, drawWidth, drawHeight), paint);
    }
}

/// <summary>Immutable wallpaper bytes and placement settings.
/// When <see cref="LiveCapture"/> is true, the bytes are a 1:1 capture of the
/// virtual desktop (physical pixels) and Style/Tile are ignored.</summary>
public record WallpaperSnapshot(byte[]? ImageBytes, SKColor Background, string Style = "10", bool Tile = false, bool LiveCapture = false)
{
    public string Describe() =>
        $"WallpaperSnapshot(bytes={ImageBytes?.Length ?? 0}, bg=#{Background.Red:X2}{Background.Green:X2}{Background.Blue:X2}, style={Style}, tile={Tile}, live={LiveCapture})";
}
