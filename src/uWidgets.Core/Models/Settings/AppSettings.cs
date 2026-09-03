namespace uWidgets.Core.Models.Settings;

/// <summary>
/// Application settings, stored in <c>appsettings.json</c>.
/// </summary>
/// <param name="Theme">Theme settings.</param>
/// <param name="Templates">Theme templates shown in the Appearance page.</param>
/// <param name="Layout">Widget sizing and positioning settings.</param>
/// <param name="Dimensions">Widget dimensions (grid unit size, margin, radius).</param>
/// <param name="Region">Region settings (language).</param>
/// <param name="RunOnStartup">Start uWidgets with Windows.</param>
/// <param name="IgnoreUpdate">Version to ignore for update checks.</param>
/// <param name="UpdateUrl">Custom update source (releases/latest page or API URL); <c>null</c>/empty disables update checks.</param>
/// <param name="Grid">Custom manual grid settings (used when <see cref="Layout.GridMode"/> is <see cref="GridMode.Manual"/>).</param>
/// <param name="HttpProxy">HTTP proxy for network requests: <c>null</c>/empty = direct connection (bypass system proxy), <c>"system"</c> = use the system proxy, otherwise a proxy URL.</param>
/// <param name="TitleBarStyle">Title bar style of the settings window (macOS traffic lights or native system buttons); <c>null</c> uses <see cref="TitleBarStyle.Native"/> so old configurations keep their look.</param>
/// <param name="TitleBarSize">Traffic light diameter in DIPs; <c>null</c> uses <see cref="DefaultTitleBarSize"/> (14 — a bit larger than the 12px macOS standard for high-DPI screens).</param>
public record AppSettings(
    Theme Theme,
    Theme[] Templates,
    Layout Layout,
    Dimensions Dimensions,
    Region Region,
    bool RunOnStartup,
    string? IgnoreUpdate,
    string? UpdateUrl = null,
    Grid? Grid = null,
    string? HttpProxy = null,
    TitleBarStyle? TitleBarStyle = null,
    double? TitleBarSize = null)
{
    /// <summary>The macOS-standard traffic light diameter (DIPs).</summary>
    public const double DefaultTitleBarSize = 14;

    /// <summary>
    /// The effective title bar style; falls back to <see cref="TitleBarStyle.Native"/>
    /// when <see cref="TitleBarStyle"/> is not set (migrates old configurations).
    /// </summary>
    public TitleBarStyle EffectiveTitleBarStyle =>
        TitleBarStyle ?? Settings.TitleBarStyle.Native;

    /// <summary>
    /// The traffic light diameter; falls back to <see cref="DefaultTitleBarSize"/>
    /// when <see cref="TitleBarSize"/> is not set.
    /// </summary>
    public double EffectiveTitleBarSize => TitleBarSize ?? DefaultTitleBarSize;
}
