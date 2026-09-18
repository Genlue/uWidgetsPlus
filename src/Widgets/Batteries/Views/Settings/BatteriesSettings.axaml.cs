using System;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Batteries.Models;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;

namespace Batteries.Views.Settings;

public partial class BatteriesSettings : UserControl
{
    private readonly IWidgetLayoutProvider widgetLayoutProvider;
    private BatteriesModel model;
    private bool isInitializing = true;

    public BatteriesSettings() : this(null!) { }

    public BatteriesSettings(IWidgetLayoutProvider? widgetLayoutProvider)
    {
        this.widgetLayoutProvider = widgetLayoutProvider!;
        model = widgetLayoutProvider != null ? (ReadModel(widgetLayoutProvider.Get()) ?? new BatteriesModel()) : new BatteriesModel();

        InitializeComponent();
        LoadFromModel();
        isInitializing = false;
    }

    private void LoadFromModel()
    {
        ShowPeripheralsSwitch.IsChecked = model.ShowPeripherals;
        ShowPercentageSwitch.IsChecked = model.ShowPercentage;
        ShowRemainingTimeSwitch.IsChecked = model.ShowRemainingTime;
        MouseBatteryInput.Value = model.MouseBattery;
        KeyboardBatteryInput.Value = model.KeyboardBattery;
        HeadphonesBatteryInput.Value = model.HeadphonesBattery;
    }

    private void OnNumericSettingChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (isInitializing) return;

        model = model with
        {
            MouseBattery = (int)(MouseBatteryInput.Value ?? 85),
            KeyboardBattery = (int)(KeyboardBatteryInput.Value ?? 68),
            HeadphonesBattery = (int)(HeadphonesBatteryInput.Value ?? 92)
        };

        Save();
    }

    private void OnToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (isInitializing) return;

        model = model with
        {
            ShowPeripherals = ShowPeripheralsSwitch.IsChecked ?? true,
            ShowPercentage = ShowPercentageSwitch.IsChecked ?? true,
            ShowRemainingTime = ShowRemainingTimeSwitch.IsChecked ?? true
        };

        Save();
    }

    private void Save()
    {
        if (widgetLayoutProvider == null) return;
        try
        {
            var layout = widgetLayoutProvider.Get();
            if (layout == null) return;
            widgetLayoutProvider.Save(layout with
            {
                Settings = JsonSerializer.SerializeToElement(model)
            });
        }
        catch { }
    }

    private static BatteriesModel? ReadModel(WidgetLayout? layout)
    {
        if (layout?.Settings is not { } settings) return null;
        if (settings.ValueKind != JsonValueKind.Object) return null;
        try
        {
            return settings.Deserialize<BatteriesModel>();
        }
        catch
        {
            return null;
        }
    }
}
