using Avalonia.Controls;
using Avalonia.Interactivity;
using Clock.Models;
using Clock.ViewModels;

namespace Clock.Views.Controls;

public partial class AnalogWorldSingle : UserControl
{
    public AnalogWorldSingle() : this(new ClockModel()) {}
    
    public AnalogWorldSingle(ClockModel clockModel)
    {
        DataContext = new AnalogClockViewModel(clockModel);
        Unloaded += (_, _) => ((AnalogClockViewModel)DataContext).Dispose();
        SizeChanged += OnSizeChanged;
        Unloaded += (_, _) => SizeChanged -= OnSizeChanged;
        InitializeComponent();
    }

    // Single-cell tiers (S, M and the 2×2 quadrants): the dial diameter drops
    // below readable size for the 12 numerals — keep hands and ring only.
    private void OnSizeChanged(object? sender, SizeChangedEventArgs e) =>
        Numbers.IsVisible = System.Math.Min(e.NewSize.Width, e.NewSize.Height) > 90;
}