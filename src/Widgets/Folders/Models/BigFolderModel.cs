using System.Text.Json.Serialization;

namespace Folders.Models;

public enum BigFolderDensity
{
    Sparse,
    Dense,
    Custom
}

/// <summary>
/// Model of the Big Folder widget, stored in <c>layout.json</c>.
/// Displays app/file icons in a clean adaptive grid without titles or names,
/// perfectly centered, supporting Sparse, Dense, and Custom (m x n) modes.
/// </summary>
/// <param name="Items">Absolute paths of pinned items (files, folders, shortcuts).</param>
/// <param name="DenseMode">Legacy density boolean (true for dense, false for sparse).</param>
/// <param name="Padding">Inner padding around the widget content, in pixels.</param>
/// <param name="Spacing">Spacing between icons in the grid, in pixels.</param>
/// <param name="Density">Grid density mode: Sparse, Dense, or Custom.</param>
/// <param name="CustomColumns">Number of columns (m) when in Custom mode.</param>
/// <param name="CustomRows">Number of rows (n) when in Custom mode.</param>
public record BigFolderModel(
    List<string>? Items = null,
    bool DenseMode = false,
    int Padding = 10,
    int Spacing = 8,
    BigFolderDensity? Density = null,
    int CustomColumns = 3,
    int CustomRows = 3,
    string? FolderName = null,
    int PopupIconSize = 40,
    int PopupSpacing = 8,
    int PopupPadding = 14,
    int PopupColumns = 4,
    bool PopupShowNames = true,
    bool PopupShowExtensions = false)
{
    [JsonIgnore]
    public List<string> SafeItems => Items ?? [];

    [JsonIgnore]
    public bool IsEmpty => SafeItems.Count == 0;

    [JsonIgnore]
    public BigFolderDensity EffectiveDensity =>
        Density ?? (DenseMode ? BigFolderDensity.Dense : BigFolderDensity.Sparse);
}
