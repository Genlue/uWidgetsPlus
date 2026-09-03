using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Weather.ViewModels;

namespace Weather.Views.Controls;

/// <summary>
/// 4×4 (large) forecast: current conditions + hourly + 7-day columns.
/// Fixed-pixel design calibrated for a ~340×340 card; scaled up via a
/// LayoutTransform on larger grids so the same layout fills big cells.
/// </summary>
public partial class ForecastLarge : UserControl
{
    private const double DesignSize = 340;

    public ForecastLarge(ForecastViewModel viewModel)
    {
        DataContext = viewModel;
        SizeChanged += OnSizeChanged;
        Unloaded += (_, _) => SizeChanged -= OnSizeChanged;
        InitializeComponent();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var scale = Math.Clamp(
            Math.Min(e.NewSize.Width, e.NewSize.Height) / DesignSize,
            1.0, 2.5);
        Scaler.LayoutTransform = scale > 1.01 ? new ScaleTransform(scale, scale) : null;
    }
}
