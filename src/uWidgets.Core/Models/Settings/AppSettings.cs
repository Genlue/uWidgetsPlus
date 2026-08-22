namespace uWidgets.Core.Models.Settings;

/// <summary>
/// Application settings, stored in <c>appsettings.json</c>.
/// </summary>
/// <param name="Theme">Theme settings.</param>
/// <param name="Templates">Theme templates shown in the Appearance page.</param>
/// <param name="Layout">Widget sizing and positioning settings.</param>
/// <param name="Dimensions">Widget dimensions (grid unit size, margin, radius, content scale).</param>
/// <param name="Region">Region settings (language).</param>
/// <param name="RunOnStartup">Start uWidgets with Windows.</param>
/// <param name="IgnoreUpdate">Version to ignore for update checks.</param>
/// <param name="UpdateUrl">Custom update source (releases/latest page or API URL); <c>null</c>/empty disables update checks.</param>
/// <param name="Grid">Custom manual grid settings (used when <see cref="Layout.GridMode"/> is <see cref="GridMode.Manual"/>).</param>
/// <param name="HttpProxy">HTTP proxy for network requests: <c>null</c>/empty = direct connection (bypass system proxy), <c>"system"</c> = use the system proxy, otherwise a proxy URL.</param>
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
    string? HttpProxy = null);
