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
        // Bubble with handledEventsToo: mouse-wheel events are bubble-routed, so a
        // tunnel handler would never fire; we intercept after the ScrollViewer and
        // translate the vertical wheel delta into horizontal offset ourselves.
        AddHandler(PointerWheelChangedEvent, OnHourlyWheel, RoutingStrategies.Bubble, true);
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var scale = Math.Clamp(
            Math.Min(e.NewSize.Width / DesignWidth, e.NewSize.Height / DesignHeight),
            1.0, 2.5);
        Scaler.LayoutTransform = scale > 1.01 ? new ScaleTransform(scale, scale) : null;
    }

    /// <summary>
    /// Mouse wheel over the horizontally scrolling hourly strip: wheel down (page
    /// scroll down) scrolls right, wheel up scrolls left.
    /// </summary>
    private void OnHourlyWheel(object? sender, PointerWheelEventArgs e)
    {
        var maxX = HourlyScroller.Extent.Width - HourlyScroller.Viewport.Width;
        if (maxX <= 0) return;

        // Bounds is in the parent's coordinate space; compare with the pointer
        // position expressed in the ScrollViewer's own local space.
        var pointer = e.GetPosition(HourlyScroller);
        if (pointer.X < 0 || pointer.Y < 0 ||
            pointer.X > HourlyScroller.Bounds.Width || pointer.Y > HourlyScroller.Bounds.Height) return;

        var delta = e.Delta.Y;
        if (delta == 0) return;

        // Wheel down (negative delta) scrolls right; wheel up scrolls left,
        // matching classic web-page scrolling.
        HourlyScroller.Offset = new Vector(
            Math.Clamp(HourlyScroller.Offset.X - delta * 60, 0, maxX),
            HourlyScroller.Offset.Y);
        e.Handled = true;
    }
}
