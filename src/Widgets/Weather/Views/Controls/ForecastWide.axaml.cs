using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Weather.ViewModels;

namespace Weather.Views.Controls;

/// <summary>
/// 4×2 (medium) forecast: current conditions + a scrolling hourly strip.
/// The fixed-pixel design is calibrated for a ~340×170 card; a LayoutTransform
/// scales it up proportionally on larger grids (cell sizes differ hugely
/// between the virtual and manual grids) while keeping the scroll behavior.
/// </summary>
public partial class ForecastWide : UserControl
{
    private const double DesignWidth = 340;
    private const double DesignHeight = 170;

    public ForecastWide(ForecastViewModel viewModel)
    {
        DataContext = viewModel;
        SizeChanged += OnSizeChanged;
        Unloaded += (_, _) => SizeChanged -= OnSizeChanged;
        InitializeComponent();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var scale = Math.Clamp(
            Math.Min(e.NewSize.Width / DesignWidth, e.NewSize.Height / DesignHeight),
            1.0, 2.5);
        Scaler.LayoutTransform = scale > 1.01 ? new ScaleTransform(scale, scale) : null;
    }
}
