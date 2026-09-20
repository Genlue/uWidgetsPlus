using uWidgets.Core.Models.Settings;

namespace Clock.Services;

/// <summary>The material a frameless clock resolved to for the current frame.</summary>
/// <param name="IsAcrylic">毛玻璃: the OS-level acrylic backdrop behind the numerals.</param>
/// <param name="IsLiquidGlass">液态玻璃: the app-rendered wallpaper lens.</param>
/// <param name="IsSolid">纯色: a plain vector fill.</param>
/// <param name="IsSoftGlow">柔光玻璃: the same pipeline as 液态玻璃 with the soft recipe.</param>
public readonly record struct FramelessMaterial(bool IsAcrylic, bool IsLiquidGlass, bool IsSolid, bool IsSoftGlow)
{
    /// <summary>
    /// True for the two materials the app renders itself from a desktop snapshot
    /// (液态玻璃 / 柔光玻璃): the numerals go through the glyph glass pipeline.
    /// </summary>
    public bool IsRenderedGlass => IsLiquidGlass || IsSoftGlow;
}

/// <summary>
/// Resolves the frameless clock's own <c>ThemeMode</c> against the global theme.
///
/// <c>ThemeMode = 0</c> means <b>follow the global theme</b>, which is the whole point of that
/// option: a global 液态玻璃 or 柔光玻璃 has to produce the glyph glass pipeline, a global 毛玻璃
/// the OS acrylic backdrop and a global 纯色 a plain fill. Exposed as a pure function (rather than
/// inlined in the view) so the mapping — and in particular that 柔光玻璃 counts as rendered glass
/// — can be asserted in <c>tests/ClockThemeChecks</c>.
/// </summary>
public static class FramelessThemeResolver
{
    /// <summary>
    /// Per-widget override: 0 = follow the global theme, 1 = acrylic, 2 = liquid glass,
    /// 3 = solid, 4 = soft glow glass.
    /// </summary>
    public static FramelessMaterial Resolve(int themeMode, Theme? globalTheme) => themeMode switch
    {
        1 => new FramelessMaterial(true, false, false, false),
        2 => new FramelessMaterial(false, true, false, false),
        3 => new FramelessMaterial(false, false, true, false),
        4 => new FramelessMaterial(false, false, false, true),
        _ => new FramelessMaterial(
            globalTheme?.UsesNativeBlur ?? true,
            globalTheme?.IsLiquidGlass ?? false,
            !(globalTheme?.UsesNativeBlur ?? true) && !(globalTheme?.UsesRenderedGlass ?? false),
            globalTheme?.IsSoftGlow ?? false)
    };
}
