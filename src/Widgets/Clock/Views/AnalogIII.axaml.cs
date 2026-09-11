using Avalonia.Controls;
using Avalonia.Interactivity;
using Clock.Models;
using Clock.ViewModels;

namespace Clock.Views;

public partial class AnalogIII : UserControl
{
    private readonly AnalogClockViewModel viewModel;

    public AnalogIII() : this(new ClockModel()) {}
    
    public AnalogIII(ClockModel clockModel)
    {
        viewModel = new AnalogClockViewModel(clockModel);
        DataContext = viewModel;
        Loaded += (_, _) => viewModel.Start();
        Unloaded += (_, _) => viewModel.Stop();
        SizeChanged += OnSizeChanged;
        InitializeComponent();
    }

    // S/M tiers: the tick ring is the core of this style and survives shrinking,
    // but the four numerals fall below readable size — drop them there.
    private void OnSizeChanged(object? sender, SizeChangedEventArgs e) =>
        Numbers.IsVisible = System.Math.Min(e.NewSize.Width, e.NewSize.Height) > 90;
}
