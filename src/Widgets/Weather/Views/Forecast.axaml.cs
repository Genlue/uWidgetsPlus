using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Weather.Models;
using Weather.ViewModels;
using Weather.Views.Controls;
using uWidgets.Services;

namespace Weather.Views;

public partial class Forecast : UserControl
{
    private readonly ForecastModel model;
    private ForecastViewModel? viewModel;

    public Forecast() : this(new ForecastModel("Beijing", 39.9042, 116.4074, "celsius")) {}
    
    public Forecast(ForecastModel model)
    {
        this.model = model;
        viewModel = new ForecastViewModel(model);
        Content = new ForecastSmall(viewModel);
        SizeChanged += OnSizeChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        InitializeComponent();
    }

    /// <summary>
    /// The view can be detached and re-added later (the settings window caches pages and the
    /// Gallery keeps a live preview control), so everything <see cref="OnUnloaded"/> released
    /// is rebuilt here — on a *new* view model, because the released one was disposed.
    /// </summary>
    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        SizeChanged -= OnSizeChanged;
        SizeChanged += OnSizeChanged;

        if (viewModel != null) return;

        var vm = new ForecastViewModel(model);
        viewModel = vm;

        // A re-added view keeps the size it already had, so SizeChanged will not fire again
        // to pick the tier content: resolve it from the size the card has right now.
        Content = CreateTierContent(Bounds.Size, vm);
    }

    /// <summary>
    /// Release the view model (its hourly timer subscription and HTTP client) without
    /// detaching this handler: the control stays usable and is unloaded again on every
    /// later removal from the visual tree.
    /// </summary>
    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        SizeChanged -= OnSizeChanged;
        viewModel?.Dispose();
        viewModel = null;
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // Detached (unloaded) views have no view model to bind the tier content to.
        var current = viewModel;
        if (current == null) return;

        Content = CreateTierContent(e.NewSize, current);
    }

    /// <summary>
    /// The content for one card size: phone-style tiers resolved from the grid span
    /// (2×2 = compact card, 4×2 = wide hourly strip, 4×4 = full daily forecast, 1×1 = the
    /// temperature only), with the historic pixel thresholds for spans that have no tier.
    /// </summary>
    private Control CreateTierContent(Size size, ForecastViewModel vm)
    {
        return SizeTiers.ResolveTier(this, size) switch
        {
            WidgetTier.Cell => new ForecastTiny(vm),
            WidgetTier.Small => new ForecastSmall(vm),
            WidgetTier.Medium => new ForecastWide(vm),
            WidgetTier.Large => new ForecastLarge(vm),
            _ => size switch
            {
                { Width: > 230, Height: > 230 } => new ForecastLarge(vm),
                { Width: > 230, Height: > 140 } => new ForecastWide(vm),
                { Width: > 75, Height: > 75 } => new ForecastSmall(vm),
                _ => new ForecastTiny(vm)
            }
        };
    }
}
