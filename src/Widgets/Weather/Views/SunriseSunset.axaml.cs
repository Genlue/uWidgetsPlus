using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Weather.Models;
using Weather.ViewModels;

namespace Weather.Views;

public partial class SunriseSunset : UserControl
{
    public SunriseSunset() : this(new ForecastModel("Beijing", 39.9042, 116.4074, "celsius")) {}

    public SunriseSunset(ForecastModel model)
    {
        DataContext = new ForecastViewModel(model);
        SizeChanged += OnSizeChanged;
        Unloaded += OnUnloaded;
        InitializeComponent();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        SizeChanged -= OnSizeChanged;
        Unloaded -= OnUnloaded;
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var size = Math.Min(DesiredSize.Width, DesiredSize.Height);
        var margin = size >= 150 ? size * 0.1 : 6;
        Margin = new Thickness(margin, margin);
    }
}