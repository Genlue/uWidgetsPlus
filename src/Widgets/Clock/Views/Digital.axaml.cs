using Avalonia.Controls;
using Avalonia.Interactivity;
using Clock.Models;
using Clock.ViewModels;

namespace Clock.Views;

public partial class Digital : UserControl
{
    private readonly DigitalClockViewModel viewModel;

    public Digital() : this(new ClockModel()) {}
    
    public Digital(ClockModel clockModel)
    {
        viewModel = new DigitalClockViewModel(clockModel);
        DataContext = viewModel;
        Unloaded += (_, _) => viewModel.Dispose();
        SizeChanged += OnSizeChanged;
        Unloaded += (_, _) => SizeChanged -= OnSizeChanged;
        InitializeComponent();
    }

    // S/M tiers: at a single-cell height the date line (natural 8/52) collapses to
    // a few pixels — show the time only and let the Viewbox use all the height.
    private void OnSizeChanged(object? sender, SizeChangedEventArgs e) =>
        Date.IsVisible = viewModel.ShowDate
                         && e.NewSize.Height > 60
                         && e.NewSize.Width > 90;
}
