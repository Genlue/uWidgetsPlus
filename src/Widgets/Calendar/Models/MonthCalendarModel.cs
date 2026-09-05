namespace Calendar.Models;

/// <param name="FirstDayOfWeek">The day the week (and the month grid) starts on.</param>
/// <param name="TodayColorMode">Today-marker color: "Accent" (follow the system accent)
/// or "Custom" (use the per-theme colors below).</param>
/// <param name="TodayColorLight">Custom today-marker color used in the light theme (#RRGGBB).</param>
/// <param name="TodayColorDark">Custom today-marker color used in the dark theme (#RRGGBB).</param>
public record MonthCalendarModel(
    DayOfWeek FirstDayOfWeek,
    string TodayColorMode = "Accent",
    string? TodayColorLight = null,
    string? TodayColorDark = null);
