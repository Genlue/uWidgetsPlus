using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FixedWidgets.Locales;
using FixedWidgets.Models;
using FixedWidgets.Services;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;

namespace FixedWidgets.Views.Settings;

public partial class AggregateSettings : UserControl
{
    private readonly IWidgetLayoutProvider widgetLayoutProvider;
    private readonly OpenMeteoWeatherService weatherService = new();
    private AggregateModel model;
    private bool isInitializing = true;

    public AggregateSettings() : this(null!)
    {
    }

    public AggregateSettings(IWidgetLayoutProvider? widgetLayoutProvider)
    {
        this.widgetLayoutProvider = widgetLayoutProvider!;
        model = widgetLayoutProvider != null ? (ReadModel(widgetLayoutProvider.Get()) ?? new AggregateModel()) : new AggregateModel();

        InitializeComponent();
        SearchBox.AsyncPopulator = SearchCity;
        InitOptions();
        LoadFromModel();
        isInitializing = false;
    }

    private void InitOptions()
    {
        UnitCombo.ItemsSource = new List<string>
        {
            Locale.Setting_Unit_Celsius,
            Locale.Setting_Unit_Fahrenheit
        };

        FirstDayCombo.ItemsSource = new List<string>
        {
            Locale.Setting_FirstDay_Monday,
            Locale.Setting_FirstDay_Sunday
        };
    }

    private void LoadFromModel()
    {
        SearchBox.Text = model.City;
        CityStatusText.Text = $"当前坐标: {model.Latitude:F2}°, {model.Longitude:F2}°";

        UnitCombo.SelectedIndex = string.Equals(model.TemperatureUnit, "fahrenheit", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        Use24HoursSwitch.IsChecked = model.Is24Hour;
        ShowSecondsSwitch.IsChecked = model.ShowSeconds;

        FirstDayCombo.SelectedIndex = model.FirstDayOfWeek == DayOfWeek.Sunday ? 1 : 0;
    }

    private async Task<IEnumerable<object>> SearchCity(string? query, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var list = await weatherService.SearchCitiesAsync(query, token);
        return list ?? [];
    }

    private void SearchBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (isInitializing || SearchBox.SelectedItem is not City city) return;

        ApplyCity(city);
    }

    private void SearchBox_OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (SearchBox.SelectedItem is City city)
            {
                ApplyCity(city);
            }
            else if (!string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                _ = ResolveCityAsync(SearchBox.Text.Trim());
            }
        }
    }

    private void SearchBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (SearchBox.SelectedItem is City city)
        {
            ApplyCity(city);
        }
        else if (!string.IsNullOrWhiteSpace(SearchBox.Text) &&
                 !string.Equals(SearchBox.Text.Trim(), model.City, StringComparison.OrdinalIgnoreCase))
        {
            _ = ResolveCityAsync(SearchBox.Text.Trim());
        }
    }

    private void ApplyCity(City city)
    {
        isInitializing = true;
        SearchBox.Text = city.Name;
        isInitializing = false;

        model = model with
        {
            City = city.Name,
            Latitude = city.Latitude,
            Longitude = city.Longitude
        };
        Save();
        CityStatusText.Text = $"已识别: {city.SearchName}";
    }

    private async Task ResolveCityAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;
        CityStatusText.Text = "正在从天气源识别...";

        var results = await weatherService.SearchCitiesAsync(query);
        if (results != null && results.Count > 0)
        {
            var matched = results[0];
            ApplyCity(matched);
        }
        else
        {
            CityStatusText.Text = "未在天气源检索到该城市";
        }
    }

    private void OnUnitChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (isInitializing || UnitCombo.SelectedIndex < 0) return;
        model = model with
        {
            TemperatureUnit = UnitCombo.SelectedIndex == 1 ? "fahrenheit" : "celsius"
        };
        Save();
    }

    private void OnToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (isInitializing) return;
        model = model with
        {
            Is24Hour = Use24HoursSwitch.IsChecked ?? true,
            ShowSeconds = ShowSecondsSwitch.IsChecked ?? false
        };
        Save();
    }

    private void OnFirstDayChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (isInitializing || FirstDayCombo.SelectedIndex < 0) return;
        model = model with
        {
            FirstDayOfWeek = FirstDayCombo.SelectedIndex == 1 ? DayOfWeek.Sunday : DayOfWeek.Monday
        };
        Save();
    }

    private static AggregateModel? ReadModel(WidgetLayout layout)
    {
        try { return layout.GetModel<AggregateModel>(); }
        catch { return null; }
    }

    private void Save()
    {
        if (widgetLayoutProvider == null) return;
        var layout = widgetLayoutProvider.Get();
        widgetLayoutProvider.Save(layout with { Settings = JsonSerializer.SerializeToElement(model) });
    }
}
