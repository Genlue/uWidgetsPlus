namespace Clock.Models;

public record WorldClockModel(
    List<string?> TimeZoneIds,
    List<string?>? CityNames = null);