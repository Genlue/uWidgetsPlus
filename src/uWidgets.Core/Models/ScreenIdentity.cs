namespace uWidgets.Core.Models;

/// <summary>
/// Runtime identity of an attached display, captured from Windows (device name,
/// EDID friendly name) and Avalonia (working-area size, scaling, primary flag).
/// Used to match stored per-screen configurations (<see cref="ScreenLayout"/>).
/// </summary>
/// <param name="DeviceName">Windows device name (e.g. <c>"\\.\DISPLAY1"</c>).</param>
/// <param name="FriendlyName">EDID friendly name (e.g. <c>"DELL U2723QE"</c>).</param>
/// <param name="Width">Working-area width (physical pixels).</param>
/// <param name="Height">Working-area height (physical pixels).</param>
/// <param name="IsPrimary">True when this is the primary screen.</param>
/// <param name="Scaling">DPI scale of the screen.</param>
public record ScreenIdentity(
    string DeviceName,
    string FriendlyName,
    int Width,
    int Height,
    bool IsPrimary,
    double Scaling)
{
    /// <summary>
    /// Auto-match key: <c>"FriendlyName|WidthxHeight"</c> — the same shape as
    /// <see cref="ScreenLayout.Key"/>.
    /// </summary>
    public string Key => $"{FriendlyName}|{Width}x{Height}";
}

/// <summary>
/// Matches stored <see cref="ScreenLayout"/> entries to attached <see cref="ScreenIdentity"/>s.
/// Priority: ① manual rebinding (<see cref="ScreenLayout.DeviceName"/>), ② identity
/// <see cref="ScreenLayout.Key"/>, ③ the legacy "primary" entry (Key = null).
/// </summary>
public static class ScreenMatcher
{
    /// <summary>
    /// Find the stored configuration for an attached screen (non-consuming; each
    /// entry may match several screens — use the consuming overload when matching
    /// a whole desktop so twin screens with identical keys do not share an entry).
    /// </summary>
    /// <returns>The matched entry, or <c>null</c> when the screen has no stored configuration.</returns>
    public static ScreenLayout? Match(IReadOnlyList<ScreenLayout> entries, ScreenIdentity screen) =>
        Match(entries, screen, null);

    /// <summary>
    /// Find the stored configuration for an attached screen, consuming the entry
    /// (its id is added to <paramref name="consumedIds"/>) so a twin screen with
    /// the SAME key never matches the same entry twice. Matching priority:
    /// ① manual device binding ② identity key ③ legacy primary entry
    /// (key = null, follows the current primary screen).
    /// </summary>
    public static ScreenLayout? Match(IReadOnlyList<ScreenLayout> entries, ScreenIdentity screen, ISet<string>? consumedIds)
    {
        ScreenLayout? Find(Func<ScreenLayout, bool> predicate) =>
            entries.FirstOrDefault(entry => predicate(entry) && (consumedIds == null || !consumedIds.Contains(entry.Id)));

        if (!string.IsNullOrEmpty(screen.DeviceName))
        {
            var bound = Find(entry => entry.DeviceName == screen.DeviceName);
            if (bound != null)
            {
                consumedIds?.Add(bound.Id);
                return bound;
            }
        }

        var byKey = Find(entry => entry.Key == screen.Key && entry.Key != null);
        if (byKey != null)
        {
            consumedIds?.Add(byKey.Id);
            return byKey;
        }

        // Legacy primary entry (Key = null) follows whichever screen is primary.
        if (screen.IsPrimary)
        {
            var legacy = Find(entry => entry.Key == null && entry.Id == ScreensLayout.LegacyPrimaryId);
            if (legacy != null)
            {
                consumedIds?.Add(legacy.Id);
                return legacy;
            }
        }

        return null;
    }
}