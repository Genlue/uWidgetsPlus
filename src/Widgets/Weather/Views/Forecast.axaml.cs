using Avalonia.Controls;
using Avalonia.Interactivity;
using Weather.Models;
using Weather.ViewModels;
using Weather.Views.Controls;
using uWidgets.Services;

namespace Weather.Views;

public partial class Forecast : UserControl
{
    private readonly ForecastViewModel viewModel;
    public Forecast() : this(new ForecastModel("Beijing", 39.9042, 116.4074, "celsius")) {}
    
    public Forecast(ForecastModel model)
    {
        viewModel = new ForecastViewModel(model);
        Content = new ForecastSmall(viewModel);
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
        // Phone-style tiers (resolved from the grid span): 2×2 = compact card,
        // 4×2 = wide hourly strip, 4×4 = full daily forecast. 1×1 shows only the
        // temperature. Other custom spans keep the historic pixel thresholds.
        switch (SizeTiers.ResolveTier(this, e.NewSize))
        {
            case WidgetTier.Cell:
                Content = new ForecastTiny(viewModel);
                return;
            case WidgetTier.Small:
                Content = new ForecastSmall(viewModel);
                return;
            case WidgetTier.Medium:
                Content = new ForecastWide(viewModel);
                return;
            case WidgetTier.Large:
                Content = new ForecastLarge(viewModel);
                return;
        }

        Content = e.NewSize switch
        {
            { Width: > 230, Height: > 230 } => new ForecastLarge(viewModel),
            { Width: > 230, Height: > 140 } => new ForecastWide(viewModel),
            { Width: > 75, Height: > 75 } => new ForecastSmall(viewModel),
            _ => new ForecastTiny(viewModel)
        };
    }
}
