namespace Clock.Models;

/// <summary>
/// Configuration model for the frameless digital clock widget.
/// </summary>
public record FramelessClockModel(
    bool Use24Hours = true,
    bool ShowSeconds = false,
    string? TimeZoneId = null,
    string? FontFamily = null,
    int FontWeight = 700,
    bool StretchFill = true,
    bool EnableOverlay = false,
    bool FollowAccentColor = true,
    string OverlayColor = "#400078D4",
    double OverlayOpacity = 0.35,
    int ThemeMode = 0)
{
    public FramelessClockModel() : this(true) { }
}
