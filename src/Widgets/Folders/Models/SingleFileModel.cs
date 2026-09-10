namespace Folders.Models;

/// <summary>
/// Model for a SingleFile widget, pinned to a single file, folder, or shortcut.
/// </summary>
/// <param name="Path">The target path of the file, folder, or shortcut.</param>
/// <param name="IconPercent">The size of the icon as a percentage of the available dimension (e.g. 70 = 70%).</param>
public record SingleFileModel(
    string? Path = null,
    double IconPercent = 70.0);
