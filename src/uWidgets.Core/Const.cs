namespace uWidgets.Core;

/// <summary>
/// Constants for the application.
/// </summary>
public static class Const
{
    /// <summary>
    /// The name of the application.
    /// </summary>
    public const string AppName = "uWidgets";
    /// <summary>
    /// The folder with the application.
    /// </summary>
    public static readonly string CurrentFolder = Path.GetDirectoryName(Environment.ProcessPath)!;
    /// <summary>
    /// The runtime data folder (settings, layout, widgets).
    /// <para>
    /// Portable mode: the folder of the exe (official layout, dev output).
    /// Packaged single-file mode: %LocalAppData%\uWidgets (extracted by <c>WidgetBundle</c>).
    /// </para>
    /// </summary>
    public static readonly string DataFolder = GetDataFolder();
    /// <summary>
    /// The folder with the widgets.
    /// </summary>
    public static readonly string WidgetsFolder = Path.Combine(DataFolder, WidgetsFolderName);
    /// <summary>
    /// The path to the application settings file.
    /// </summary>
    public static readonly string AppSettingsFile = Path.Combine(DataFolder, AppSettingsFileName);
    /// <summary>
    /// The path to the layout file.
    /// </summary>
    public static readonly string LayoutFile = Path.Combine(DataFolder, LayoutFileName);

    private static string WidgetsFolderName => "Widgets";
    private static string AppSettingsFileName => "appSettings.json";
    private static string LayoutFileName => "layout.json";

    private static string GetDataFolder()
    {
        // Portable mode: a Widgets folder next to the exe (official layout / dev output).
        if (Directory.Exists(Path.Combine(CurrentFolder, WidgetsFolderName)))
            return CurrentFolder;

        // Packaged single-file mode: runtime data lives in LocalAppData (writable, hot-updatable).
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppName);
    }
}
