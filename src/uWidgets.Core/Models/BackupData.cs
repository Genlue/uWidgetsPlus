using uWidgets.Core.Models.Settings;

namespace uWidgets.Core.Models;

/// <summary>
/// A backup file: the application settings plus the full widget layout.
/// <para>
/// Covers everything the user asked to back up: the app settings (theme,
/// dimensions, proxy, title bar …), the manual grid configuration (the
/// <see cref="AppSettings.Grid"/> section of the app settings), the card
/// layout (each <see cref="WidgetLayout"/> X/Y/Width/Height) and the per-card
/// content settings (each <see cref="WidgetLayout.Settings"/>).
/// </para>
/// </summary>
/// <param name="Version">Backup format version (<see cref="CurrentVersion"/>).</param>
/// <param name="AppSettings">Application settings (incl. the manual grid config).</param>
/// <param name="Layout">Widget layout: card positions/sizes + per-card content settings.</param>
public record BackupData(int Version, AppSettings AppSettings, List<WidgetLayout> Layout)
{
    /// <summary>
    /// The current backup format version. Bump it when the structure changes so
    /// older backups are rejected instead of being misread.
    /// </summary>
    public const int CurrentVersion = 1;
}