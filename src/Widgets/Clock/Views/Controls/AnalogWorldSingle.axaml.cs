using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
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
        InitializeComponent();
        BuildTicks();
    }

    /// <summary>
    /// When false, the 12 numerals are hidden for tiny dials (tick marks stay).
    /// </summary>
    public bool ShowNumbers
    {
        get => Numbers.IsVisible;
        set => Numbers.IsVisible = value;
    }

    private void BuildTicks()
    {
        if (Ticks.Children.Count > 0) return;

        var brush = Application.Current != null
            && Application.Current.TryFindResource("SystemControlForegroundBaseHighBrush", out var resource)
            && resource is IBrush b
            ? b
            : Brushes.Gray;

        for (var i = 0; i < 12; i++)
        {
            var angle = i * 30.0 * Math.PI / 180.0;
            var inner = 425.0;
            var outer = 470.0;
            var x1 = 500.0 + Math.Sin(angle) * inner;
            var y1 = 500.0 - Math.Cos(angle) * inner;
            var x2 = 500.0 + Math.Sin(angle) * outer;
            var y2 = 500.0 - Math.Cos(angle) * outer;
            Ticks.Children.Add(new Avalonia.Controls.Shapes.Path
            {
                Data = new LineGeometry(new Point(x1, y1), new Point(x2, y2)),
                Stroke = brush,
                StrokeThickness = 14,
                StrokeLineCap = PenLineCap.Round,
                Opacity = 0.35,
                IsHitTestVisible = false
            });
        }
    }
}
