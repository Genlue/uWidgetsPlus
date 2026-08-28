using System.Linq;
using System.Text.Json.Serialization;
using uWidgets.Core.Models.Settings;

namespace uWidgets.Core.Models;

/// <summary>
/// Per-screen widget configuration, stored in <c>layout.json</c> under the
/// <see cref="ScreensLayout.Screens"/> collection.
/// </summary>
/// <param name="Id">Stable screen id (GUID, generated once when the screen first appears). Used for manual rebinding and screen alias.</param>
/// <param name="Key">Auto-match identity: <c>"FriendlyName|WidthxHeight"</c> (e.g. <c>"DELL U2723QE|2560x1600"</c>). <c>null</c> = "legacy primary" entry (matches the current primary screen).</param>
/// <param name="Alias">User alias shown in the UI (e.g. <c>"主屏"</c>, <c>"观影屏"</c>); <c>null</c> falls back to the friendly name.</param>
/// <param name="DeviceName">Windows device name (e.g. <c>"\\.\DISPLAY1"</c>). Set by a manual screen rebinding; <c>null</c> = auto-match by <see cref="Key"/>.</param>
/// <param name="Grid">This screen's manual grid (percent-based). <c>null</c> falls back to <see cref="AppSettings.Grid"/>, then <see cref="Grid.Default"/>.</param>
/// <param name="ContentScale">This screen's content scale. <c>null</c> falls back to <see cref="Dimensions.ContentScale"/>.</param>
/// <param name="Layout">Widgets placed on this screen (positions relative to the screen's working area).</param>
public record ScreenLayout(
    string Id,
    string? Key,
    string? Alias,
    string? DeviceName,
    Grid? Grid,
    double? ContentScale,
    List<WidgetLayout> Layout)
{
    /// <summary>
    /// Display name for the UI: the user alias when set, otherwise the friendly name part of the <see cref="Key"/>.
    /// </summary>
    [JsonIgnore]
    public string DisplayName => Alias ?? Key?.Split('|')[0] ?? "Primary";
}

/// <summary>
/// Multi-screen layout file (format v2): a collection of per-screen configurations.
/// <para>
/// v1 files (a plain <see cref="WidgetLayout"/> array) are still readable — they are
/// wrapped as a single "legacy primary" entry (<see cref="ScreenLayout.Key"/> = null,
/// which matches whichever screen is primary at runtime). Saving always writes v2.
/// </para>
/// </summary>
public record ScreensLayout(List<ScreenLayout> Screens, int Version = 2)
{
    /// <summary>Id of the legacy v1 primary entry.</summary>
    public const string LegacyPrimaryId = "primary";

    /// <summary>
    /// Wrap a legacy v1 layout list as a single primary-screen entry.
    /// </summary>
    public static ScreensLayout FromLegacy(List<WidgetLayout> layout) =>
        new([new ScreenLayout(LegacyPrimaryId, null, null, null, null, null, layout)], Version: 1);

    public ScreenLayout? FindById(string id) =>
        Screens.FirstOrDefault(screen => screen.Id == id);

    public ScreensLayout WithScreen(ScreenLayout screen) => this with
    {
        Screens = Screens.Select(item => item.Id == screen.Id ? screen : item).ToList()
    };

    public ScreensLayout AddScreen(ScreenLayout screen) => this with { Screens = [..Screens, screen] };

    public ScreensLayout UpsertScreen(ScreenLayout screen) =>
        Screens.Any(item => item.Id == screen.Id) ? WithScreen(screen) : AddScreen(screen);

    /// <summary>
    /// Remove redundant duplicate entries: two entries sharing the same identity
    /// <see cref="ScreenLayout.Key"/> are normally twin screens (each keeps its own
    /// layout), but a duplicated empty entry can appear when a screen is matched /
    /// created twice (stale in-memory list). Entries with identical keys are kept
    /// only when BOTH carry widgets; an empty duplicate is dropped in favor of the
    /// entry that owns the widgets (or is aliased) — never merges two layouts.
    /// </summary>
    public ScreensLayout Deduplicate()
    {
        var kept = new List<ScreenLayout>();
        var changed = false;
        foreach (var screen in Screens)
        {
            // The legacy primary entry (Key = null) is always unique.
            if (screen.Key == null)
            {
                kept.Add(screen);
                continue;
            }

            var existing = kept.FirstOrDefault(item => item.Key == screen.Key);
            if (existing == null)
            {
                kept.Add(screen);
                continue;
            }

            // Identical key: prefer the entry with widgets; an empty duplicate is
            // dropped unless it is the only one with an alias.
            if (screen.Layout.Count == 0 && existing.Layout.Count > 0) { changed = true; continue; }
            if (existing.Layout.Count == 0 && screen.Layout.Count > 0)
            {
                kept[kept.IndexOf(existing)] = screen;
                changed = true;
                continue;
            }
            if (existing.Layout.Count == 0 && screen.Layout.Count == 0)
            {
                if (screen.Alias != null && existing.Alias == null)
                {
                    // Keep the aliased twin (the user's named screen), drop the bare one.
                    kept[kept.IndexOf(existing)] = screen;
                }
                // Either way an empty duplicate is dropped → the set changed.
                changed = true;
                continue;
            }

            // Both carry widgets → genuine twin screens; keep both.
            kept.Add(screen);
        }

        // Return the SAME instance when nothing was dropped so callers can test
        // for an actual change with ReferenceEquals (avoiding needless re-saves).
        return changed ? this with { Screens = kept } : this;
    }

    /// <summary>
    /// All widgets across every screen (flat, for gallery / unload checks / legacy consumers).
    /// </summary>
    [JsonIgnore]
    public List<WidgetLayout> AllWidgets => Screens.SelectMany(screen => screen.Layout).ToList();
}