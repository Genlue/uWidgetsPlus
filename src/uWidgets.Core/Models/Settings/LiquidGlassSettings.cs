namespace uWidgets.Core.Models.Settings;

/// <summary>
/// Static glass optics, shared by the two wallpaper-sampled materials
/// (<see cref="SurfaceStyle.LiquidGlass"/> and <see cref="SurfaceStyle.SoftGlow"/>).
/// Distances are in DIPs; strengths are in percent.
/// </summary>
/// <param name="Blur">Backdrop blur strength (0-100); the renderer maps it to a Gaussian sigma.</param>
/// <param name="Refraction">Lens displacement strength (0-100%).</param>
/// <param name="EdgeWidth">
/// Lens band width in DIPs (0-80): how far the refraction reaches inwards.
/// <c>0</c> removes the lens ring entirely — the material then only diffuses, dyes and
/// lights the rim (a pure 柔光 / frosted-glass look with no bending).
/// </param>
/// <param name="Highlight">Rim line + specular strength (0-100).</param>
/// <param name="Dispersion">Chromatic dispersion strength (0-100).</param>
/// <param name="LightAngle">Light direction in degrees (0-360; 225 = top-left).</param>
/// <param name="EdgeTint">Edge colour tint strength (0-100%) — the soft colored rim at the glass border.</param>
/// <param name="WallpaperOffsetX">Manual wallpaper alignment offset in DIPs.</param>
/// <param name="WallpaperOffsetY">Manual wallpaper alignment offset in DIPs.</param>
/// <param name="Glow">
/// 柔光晕 strength (0-100%) of <see cref="SurfaceStyle.SoftGlow"/>: a broad, diffused
/// bloom around the rim instead of the crisp highlight line. <c>0</c> (default) keeps
/// the 液态玻璃 look, so old configurations and the LiquidGlass preset are unchanged.
/// </param>
/// <param name="Spectrum">
/// Spectral dispersion (0-100%) of <see cref="SurfaceStyle.SoftGlow"/>: blends the
/// three-tap RGB split towards a seven-tap spectrum (red→violet). <c>0</c> (default)
/// keeps the historic three-tap dispersion.
/// </param>
/// <param name="DyeSpread">
/// 染色扩散: how far the edge dye of <see cref="SurfaceStyle.SoftGlow"/> reaches
/// <b>inwards</b> (0-100%). <c>0</c> keeps the colour in the outermost band only —
/// a clean dyed rim with no wash over the content — while <c>100</c> spreads it deep
/// into the card. It scales both the dye band's width and the ambient (interior)
/// share of the aura; the sideways 晕染 between neighbouring rim colours is
/// unaffected. Ignored by <see cref="SurfaceStyle.LiquidGlass"/>.
/// </param>
public record LiquidGlassSettings(
    double Blur = 12,
    double Refraction = 28,
    double EdgeWidth = 24,
    double Highlight = 65,
    double Dispersion = 18,
    double LightAngle = 225,
    double EdgeTint = 50,
    double WallpaperOffsetX = 0,
    double WallpaperOffsetY = 0,
    double Glow = 0,
    double Spectrum = 0,
    double DyeSpread = 35)   // = DefaultDyeSpread; a primary-constructor default cannot name it
{
    /// <summary>
    /// Default 染色扩散 (%). Deliberately moderate: a wide wash over the content reads as
    /// dirty, so a stored theme that predates the knob gets a rim-confined dye rather than
    /// the deepest spread.
    /// </summary>
    public const double DefaultDyeSpread = 35;

    /// <summary>Dye band width as a fraction of the card's short side, at 染色扩散 = 0 / 100.</summary>
    public const double MinDyeBandFraction = 0.04;

    /// <summary>Dye band width as a fraction of the card's short side at 染色扩散 = 100.</summary>
    public const double MaxDyeBandFraction = 0.14;
    /// <summary>Default edge tint strength (%) — the soft colored rim at the glass border.</summary>
    public const double DefaultEdgeTint = 50;

    /// <summary>How much the auto-derived rim color is chroma-boosted (0-1).</summary>
    public const double DefaultEdgeTintChromaBoost = 0.55;

    /// <summary>Manual wallpaper alignment limit (DIPs) for the calibration dialog.</summary>
    public const double WallpaperOffsetLimit = 1000;

    /// <summary>
    /// Factory optics of the 柔光玻璃 preset: gentle, wide refraction with a diffused
    /// luminous rim (Glow) and a fine spectral dispersion (Spectrum). The heavy lifting
    /// is done by the renderer's soft-glow recipe; these values only steer its intensity.
    /// </summary>
    public static LiquidGlassSettings SoftGlowPreset { get; } = new(
        Blur: 10,
        Refraction: 16,
        EdgeWidth: 40,
        Highlight: 46,
        Dispersion: 24,
        LightAngle: 225,
        EdgeTint: 40,
        WallpaperOffsetX: 0,
        WallpaperOffsetY: 0,
        Glow: 70,
        Spectrum: 62,
        DyeSpread: 55);

    /// <summary>Keep imported or hand-edited values finite and within the UI ranges.</summary>
    public LiquidGlassSettings Normalize() => this with
    {
        Blur = Clamp(Blur, 0, 100, 12),
        Refraction = Clamp(Refraction, 0, 100, 28),
        EdgeWidth = Clamp(EdgeWidth, 0, 80, 24),
        Highlight = Clamp(Highlight, 0, 100, 65),
        Dispersion = Clamp(Dispersion, 0, 100, 18),
        LightAngle = Clamp(LightAngle, 0, 360, 225),
        EdgeTint = Clamp(EdgeTint, 0, 100, DefaultEdgeTint),
        WallpaperOffsetX = Clamp(WallpaperOffsetX, -WallpaperOffsetLimit, WallpaperOffsetLimit, 0),
        WallpaperOffsetY = Clamp(WallpaperOffsetY, -WallpaperOffsetLimit, WallpaperOffsetLimit, 0),
        Glow = Clamp(Glow, 0, 100, 0),
        Spectrum = Clamp(Spectrum, 0, 100, 0),
        DyeSpread = Clamp(DyeSpread, 0, 100, DefaultDyeSpread)
    };

    private static double Clamp(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
