using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Weather.Models;
using Weather.ViewModels;

namespace Weather.Views;

public partial class Pressure : UserControl
{
    private readonly ForecastModel model;
    private ForecastViewModel? viewModel;

    public Pressure() : this(new ForecastModel("Beijing", 39.9042, 116.4074, "celsius")) {}

    public Pressure(ForecastModel model)
    {
        this.model = model;
        viewModel = new ForecastViewModel(model);
        DataContext = viewModel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        InitializeComponent();
    }

    /// <summary>
    /// The view can be detached and re-added later (the settings window caches pages and the
    /// Gallery keeps a live preview control), so the view model released by
    /// <see cref="OnUnloaded"/> is rebuilt here instead of being reused after disposal.
    /// </summary>
    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (viewModel != null) return;
        viewModel = new ForecastViewModel(model);
        DataContext = viewModel;
    }

    /// <summary>
    /// Release the view model (its hourly timer subscription and HTTP client) without
    /// detaching this handler: the control stays usable and is unloaded again on every
    /// later removal from the visual tree.
    /// </summary>
    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        viewModel?.Dispose();
        viewModel = null;
    }
}
