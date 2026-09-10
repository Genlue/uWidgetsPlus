namespace uWidgets.Core.Models.Settings;

/// <summary>Static glass optics. Distances are in DIPs; strengths are in percent.</summary>
public record LiquidGlassSettings(
    double Blur = 12,
    double Refraction = 28,
    double EdgeWidth = 24,
    double Highlight = 65,
    double Dispersion = 18,
    double LightAngle = 225,
    double EdgeTint = 50,
    double WallpaperOffsetX = 0,
    double WallpaperOffsetY = 0)
{
    /// <summary>Default edge tint strength (%) — the soft colored rim at the glass border.</summary>
    public const double DefaultEdgeTint = 50;

    /// <summary>How much the auto-derived rim color is chroma-boosted (0-1).</summary>
    public const double DefaultEdgeTintChromaBoost = 0.55;

    /// <summary>Manual wallpaper alignment limit (DIPs) for the calibration dialog.</summary>
    public const double WallpaperOffsetLimit = 1000;

    /// <summary>Keep imported or hand-edited values finite and within the UI ranges.</summary>
    public LiquidGlassSettings Normalize() => this with
    {
        Blur = Clamp(Blur, 0, 100, 12),
        Refraction = Clamp(Refraction, 0, 100, 28),
        EdgeWidth = Clamp(EdgeWidth, 4, 80, 24),
        Highlight = Clamp(Highlight, 0, 100, 65),
        Dispersion = Clamp(Dispersion, 0, 100, 18),
        LightAngle = Clamp(LightAngle, 0, 360, 225),
        EdgeTint = Clamp(EdgeTint, 0, 100, DefaultEdgeTint),
        WallpaperOffsetX = Clamp(WallpaperOffsetX, -WallpaperOffsetLimit, WallpaperOffsetLimit, 0),
        WallpaperOffsetY = Clamp(WallpaperOffsetY, -WallpaperOffsetLimit, WallpaperOffsetLimit, 0)
    };

    private static double Clamp(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
