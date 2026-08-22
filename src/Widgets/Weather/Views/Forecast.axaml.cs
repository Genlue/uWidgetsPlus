using Avalonia.Controls;
using Avalonia.Interactivity;
using Weather.Models;
using Weather.ViewModels;
using Weather.Views.Controls;

namespace Weather.Views;

public partial class Forecast : UserControl
{
    private readonly ForecastViewModel viewModel;
    public Forecast() : this(new ForecastModel("Cupertino", 37.3230, -122.0322, "celsius")) {}
    
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
        // The small layout scales itself to its slot (see ForecastSmall) and only
        // falls back to the temperature-only tiny layout for genuinely tiny cards
        // (a single grid cell on any DPI scale).
        Content = e.NewSize switch
        {
            { Width: > 230, Height: > 230 } => new ForecastLarge(viewModel),
            { Width: > 230, Height: > 140 } => new ForecastWide(viewModel),
            { Width: > 75, Height: > 75 } => new ForecastSmall(viewModel),
            _ => new ForecastTiny(viewModel)
        };
    }
}