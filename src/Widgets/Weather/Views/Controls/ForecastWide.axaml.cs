using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Weather.ViewModels;

namespace Weather.Views.Controls;

/// <summary>
/// 4x2 (medium) forecast: current conditions + a scrolling hourly strip.
/// The fixed-pixel design is calibrated for a ~340x170 card; a LayoutTransform
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
        AddHandler(PointerWheelChangedEvent, OnHourlyWheel, RoutingStrategies.Tunnel);
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var scale = Math.Clamp(
            Math.Min(e.NewSize.Width / DesignWidth, e.NewSize.Height / DesignHeight),
            1.0, 2.5);
        Scaler.LayoutTransform = scale > 1.01 ? new ScaleTransform(scale, scale) : null;
    }

    /// <summary>
    /// Mouse wheel over the horizontally scrolling hourly strip: wheel up scrolls
    /// right, wheel down scrolls left (mirrors the horizontal drag direction).
    /// </summary>
    private void OnHourlyWheel(object? sender, PointerWheelEventArgs e)
    {
        var maxX = HourlyScroller.Extent.Width - HourlyScroller.Viewport.Width;
        if (maxX <= 0) return;
        if (!HourlyScroller.Bounds.Contains(e.GetPosition(HourlyScroller))) return;

        var delta = e.Delta.Y;
        if (delta == 0) return;

        HourlyScroller.Offset = new Vector(
            Math.Clamp(HourlyScroller.Offset.X + delta * 60, 0, maxX),
            HourlyScroller.Offset.Y);
        e.Handled = true;
    }
}
