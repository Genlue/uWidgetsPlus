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
/// <param name="OutlineColor">
/// Highlight ring color of the glass outline, in HEX format (RGBA, alpha is kept).
/// <c>null</c> uses <see cref="DefaultOutlineColor"/>.
/// </param>
/// <param name="OutlineWidth">
/// Highlight ring thickness in DIPs for the frosted glass; <c>0</c> (default)
/// hides the ring. The outline is part of the 毛玻璃 theme — any glass surface
/// draws it when this is greater than 0.
/// </param>
/// <param name="SolidBackgroundDark">
/// 纯色 surface background color in dark mode, HEX format. <c>null</c> uses
/// <see cref="DefaultSolidBackgroundDark"/>.
/// </param>
/// <param name="SolidBackgroundLight">
/// 纯色 surface background color in light mode, HEX format. <c>null</c> uses
/// <see cref="DefaultSolidBackgroundLight"/>.
/// </param>
/// <param name="MonochromeVariant">
/// Monochrome color source when <see cref="Monochrome"/> is enabled.
/// <c>null</c> uses <see cref="DefaultMonochromeVariant"/> (强调色, the historic
/// behavior, so old configurations keep their look).
/// </param>
/// <param name="AutoTheme">
/// Should the light/dark mode be chosen automatically from the desktop wallpaper
/// (dark wallpaper → dark mode, light wallpaper → light mode). When enabled,
/// <see cref="DarkMode"/> is ignored.
/// </param>
public record Theme(
    bool? DarkMode, 
    string? AccentColor, 
    double OpacityLevel, 
    bool Monochrome, 
    bool UseNativeFrame, 
    string FontFamily,
    SurfaceStyle? Surface = null,
    string? OutlineColor = null,
    double OutlineWidth = 0,
    string? SolidBackgroundDark = null,
    string? SolidBackgroundLight = null,
    MonochromeStyle? MonochromeVariant = null,
    bool AutoTheme = false)
{
    /// <summary>Default highlight-ring color when <see cref="OutlineColor"/> is not set
    /// (soft gray-white, less stark than pure white).</summary>
    public const string DefaultOutlineColor = "#B3FFFFFF";

    /// <summary>Default highlight-ring thickness (DIPs): 0 = outline hidden.</summary>
    public const double DefaultOutlineWidth = 0;

    /// <summary>Default 纯色 background in dark mode (dark gray, the historic look).</summary>
    public const string DefaultSolidBackgroundDark = "#2E2E2E";

    /// <summary>Default 纯色 background in light mode (white, the historic look).</summary>
    public const string DefaultSolidBackgroundLight = "#FFFFFF";

    /// <summary>Default monochrome color source (强调色, the historic look).</summary>
    public const MonochromeStyle DefaultMonochromeVariant = MonochromeStyle.Accent;

    /// <summary>
    /// The effective surface material. Falls back to deriving it from
    /// <see cref="OpacityLevel"/> when <see cref="Surface"/> is not set
    /// (migrates old configurations that predate the <see cref="SurfaceStyle"/> field).
    /// </summary>
    public SurfaceStyle EffectiveSurface =>
        Surface ?? (OpacityLevel < 1 ? SurfaceStyle.Acrylic : SurfaceStyle.Solid);

    /// <summary>
    /// True for surfaces that use a translucent, blur-capable backdrop
    /// (frosted glass and its outlined variant); false for <see cref="SurfaceStyle.Solid"/>.
    /// </summary>
    public bool IsGlass => EffectiveSurface != SurfaceStyle.Solid;

    /// <summary>
    /// The highlighted glass ring color; falls back to <see cref="DefaultOutlineColor"/>
    /// when <see cref="OutlineColor"/> is not set.
    /// </summary>
    public string EffectiveOutlineColor => OutlineColor ?? DefaultOutlineColor;

    /// <summary>
    /// The 纯色 background color in dark mode; falls back to
    /// <see cref="DefaultSolidBackgroundDark"/> when <see cref="SolidBackgroundDark"/>
    /// is not set.
    /// </summary>
    public string EffectiveSolidBackgroundDark => SolidBackgroundDark ?? DefaultSolidBackgroundDark;

    /// <summary>
    /// The 纯色 background color in light mode; falls back to
    /// <see cref="DefaultSolidBackgroundLight"/> when <see cref="SolidBackgroundLight"/>
    /// is not set.
    /// </summary>
    public string EffectiveSolidBackgroundLight => SolidBackgroundLight ?? DefaultSolidBackgroundLight;

    /// <summary>
    /// The monochrome color source; falls back to <see cref="DefaultMonochromeVariant"/>
    /// when <see cref="MonochromeVariant"/> is not set.
    /// </summary>
    public MonochromeStyle EffectiveMonochromeVariant => MonochromeVariant ?? DefaultMonochromeVariant;
}
