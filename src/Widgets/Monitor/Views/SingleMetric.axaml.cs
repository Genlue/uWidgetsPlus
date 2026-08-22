using Avalonia.Controls;
using Avalonia.Interactivity;
using Monitor.Models;
using Monitor.ViewModels;

namespace Monitor.Views;

public partial class SingleMetric : UserControl
{
    public SingleMetric() :
        this(new SingleMetricModel(MetricType.CpuUsage)) {}
    
    public SingleMetric(SingleMetricModel model)
    {
        DataContext = new SingleMetricViewModel(model);
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
        var size = e.NewSize;

        // Landscape (2×1, 3×2, …): gauge left, percentage right, whole canvas
        // scales to the card so the proportions stay right.
        var landscape = size.Width > size.Height * 1.25;

        // Tiny: a single square grid cell at typical DPI scales has no room for
        // the percentage text in any arrangement — gauge only, tighter margin.
        var tiny = !landscape && size.Height < 80;

        Landscape.IsVisible = landscape && !tiny;

        if (tiny)
        {
            Stacked.IsVisible = true;
            Stacked.RowDefinitions = RowDefinitions.Parse("*");
            Grid.SetRow(Ring, 0);
            Grid.SetRow(Text, 0);
            Text.IsVisible = false;
            Margin = new(6);
        }
        else if (landscape)
        {
            Stacked.IsVisible = false;
            Margin = new(12);
        }
        else
        {
            Stacked.IsVisible = true;
            Stacked.RowDefinitions = RowDefinitions.Parse("*,*");
            Grid.SetRow(Ring, 0);
            Grid.SetRow(Text, 1);
            Text.IsVisible = true;
            Margin = new(12);
        }
    }
}
