using System.Globalization;
using System.Text.Json;
using uWidgets.Core.Services;
using Weather.Models.Forecast;
using Weather.Models.Geocoding;

namespace Weather.Services;

public class OpenMeteoWeatherProvider
{
    // Follows the configurable proxy setting (default: direct connection, bypassing
    // the system proxy — a stale proxy from a shut-down proxy client used to break
    // weather updates). See ProxySettings.
    private readonly HttpClient httpClient = ProxySettings.CreateHttpClient();

    public async Task<ForecastResponse?> GetForecastAsync(double latitude, double longitude, string temperatureUnit)
    {
        try
        {
            var latitudeString = string.Format(CultureInfo.InvariantCulture, "{0:F4}", latitude);
            var longitudeString = string.Format(CultureInfo.InvariantCulture, "{0:F4}", longitude);
            
            var url = $"https://api.open-meteo.com/v1/forecast?" +
                      $"latitude={latitudeString}&" +
                      $"longitude={longitudeString}&" +
                      $"temperature_unit={temperatureUnit}&" +
                      $"current=temperature_2m,weathercode,surface_pressure&" +
                      $"hourly=temperature_2m,weathercode,uv_index,is_day&" +
                      $"daily=temperature_2m_min,temperature_2m_max,weathercode,sunrise,sunset&" +
                      $"timezone=auto";
            
            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode) throw new Exception(JsonSerializer.Serialize(response));

            var json = await response.Content.ReadAsStringAsync();
            var forecast = JsonSerializer.Deserialize<ForecastResponse>(json);

            return forecast;
        }
        catch (Exception e)
        {
            await File.WriteAllTextAsync("weather_crash_log.txt", $"{e.Message}{Environment.NewLine}{e.StackTrace}");
            return null;
        }
    }

    public async Task<AirQualityResponse?> GetAirQualityAsync(double latitude, double longitude)
    {
        try
        {
            var latitudeString = string.Format(CultureInfo.InvariantCulture, "{0:F4}", latitude);
            var longitudeString = string.Format(CultureInfo.InvariantCulture, "{0:F4}", longitude);

            var url = $"https://air-quality-api.open-meteo.com/v1/air-quality?" +
                      $"latitude={latitudeString}&" +
                      $"longitude={longitudeString}&" +
                      $"current=european_aqi";
            
            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode) throw new Exception(JsonSerializer.Serialize(response));

            var json = await response.Content.ReadAsStringAsync();
            var forecast = JsonSerializer.Deserialize<AirQualityResponse>(json);

            return forecast;
        }
        catch (Exception e)
        {
            await File.WriteAllTextAsync("weather_crash_log.txt", $"{e.Message}{Environment.NewLine}{e.StackTrace}");
            return null;
        }
    } 

    public async Task<List<City>?> GetCitiesAsync(string cityName)
    {
        try
        {
            var language = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
            
            var url = $"https://geocoding-api.open-meteo.com/v1/search?" +
                      $"name={cityName}&" +
                      $"language={language}";

            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var searchResults = JsonSerializer.Deserialize<GeocodingResponse>(json);

            return searchResults?.Cities;
        }
        catch (Exception e)
        {
            await File.WriteAllTextAsync("weather_crash_log.txt", $"{e.Message}{Environment.NewLine}{e.StackTrace}");
            return null;
        }
    }
}