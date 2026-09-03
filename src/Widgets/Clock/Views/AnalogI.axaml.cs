using Avalonia.Controls;
using Avalonia.Interactivity;
using Clock.Models;
using Clock.ViewModels;

namespace Clock.Views;

public partial class AnalogI : UserControl
{
    public AnalogI() : this(new ClockModel()) {}
    
    public AnalogI(ClockModel clockModel)
    {
        DataContext = new AnalogClockViewModel(clockModel);
        Unloaded += (_, _) => ((AnalogClockViewModel)DataContext).Dispose();
        SizeChanged += OnSizeChanged;
        Unloaded += (_, _) => SizeChanged -= OnSizeChanged;
        InitializeComponent();
    }

    // S/M tiers: the dial shrinks to a single-cell diameter and the 12 numbers
    // become illegible — keep just the ring, strokes and hands.
    private void OnSizeChanged(object? sender, SizeChangedEventArgs e) =>
        Numbers.IsVisible = System.Math.Min(e.NewSize.Width, e.NewSize.Height) > 90;
}
