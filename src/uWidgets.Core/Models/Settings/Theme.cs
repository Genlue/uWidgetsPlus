namespace uWidgets.Core.Models.Settings;

/// <summary>
/// Application theme settings.
/// </summary>
/// <param name="DarkMode">
/// Should the application use the dark mode
/// <para><c>null</c> to use system settings</para>
/// </param>
/// <param name="AccentColor">
/// Accent color in HEX format
/// <para><c>null</c> to use system accent color</para>
/// </param>
/// <param name="OpacityLevel">
/// Widget's background opacity level
/// </param>
/// <param name="Monochrome">
/// Should the application use monochrome theme
/// </param>
/// <param name="UseNativeFrame">
/// Should the application use native window frame
/// </param>
/// <param name="FontFamily">
/// Font family to use
/// </param>
/// <param name="Surface">
/// Widget surface material. Null means "not explicitly set"; the effective
/// material is then derived from <see cref="OpacityLevel"/>. Preferred over the
/// raw <see cref="OpacityLevel"/>&gt;1 heuristic once liquid glass is added.
/// </param>
public record Theme(
    bool? DarkMode, 
    string? AccentColor, 
    double OpacityLevel, 
    bool Monochrome, 
    bool UseNativeFrame, 
    string FontFamily,
    SurfaceStyle? Surface = null)
{
    /// <summary>
    /// The effective surface material. Falls back to deriving it from
    /// <see cref="OpacityLevel"/> when <see cref="Surface"/> is not set
    /// (migrates old configurations that predate the <see cref="SurfaceStyle"/> field).
    /// </summary>
    public SurfaceStyle EffectiveSurface =>
        Surface ?? (OpacityLevel < 1 ? SurfaceStyle.Acrylic : SurfaceStyle.Solid);
}