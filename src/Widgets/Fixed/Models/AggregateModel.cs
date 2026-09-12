using System;

namespace FixedWidgets.Models;

public record AggregateModel(
    string City = "北京",
    double Latitude = 39.9042,
    double Longitude = 116.4074,
    string TemperatureUnit = "celsius",
    bool Is24Hour = true,
    DayOfWeek FirstDayOfWeek = DayOfWeek.Monday,
    bool ShowSeconds = false,
    string? CustomAccentColor = null)
{
    public AggregateModel Copy() => this with { };
}
