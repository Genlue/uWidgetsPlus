namespace Notes.Models;

/// <summary>
/// Where the note's body comes from.
/// </summary>
public enum NoteSource
{
    /// <summary>The text stored inside the widget's own settings.</summary>
    Internal,

    /// <summary>A single markdown file on disk.</summary>
    File,

    /// <summary>All (or the most recent / explicitly picked) .md files of a folder.</summary>
    Folder,
}

/// <param name="Title">Widget title (internal/folder modes).</param>
/// <param name="Content">Note body (internal mode).</param>
/// <param name="Updated">Internal-mode "last updated" stamp.</param>
/// <param name="Markdown">Render the body as markdown (double-click to edit the source).</param>
/// <param name="FollowAccentHeader">The title bar follows the app accent color.</param>
/// <param name="HeaderColor">Custom title bar color (hex, e.g. #3376CD) when not following the accent.</param>
/// <param name="HeaderOpacity">Title bar opacity (0–1), applied in both color modes.</param>
/// <param name="Source">Body source: internal text, one file, or a folder of files.</param>
/// <param name="Path">Target file or folder for <see cref="Source"/>.</param>
/// <param name="RecentFiles">Folder mode: take the most recently modified documents.</param>
/// <param name="DocumentCount">Folder mode: how many documents to show.</param>
/// <param name="SelectedFiles">Folder mode: explicit file names (when <see cref="RecentFiles"/> is off).</param>
/// <param name="BodyPadding">Left/right inner padding of the note body text (DIPs).</param>
public record NoteModel(
    string? Title = null,
    string? Content = null,
    DateTime? Updated = null,
    bool Markdown = true,
    bool FollowAccentHeader = true,
    string? HeaderColor = null,
    double HeaderOpacity = 0.15,
    NoteSource Source = NoteSource.Internal,
    string? Path = null,
    bool RecentFiles = true,
    int DocumentCount = 3,
    List<string>? SelectedFiles = null,
    int BodyPadding = 4);
