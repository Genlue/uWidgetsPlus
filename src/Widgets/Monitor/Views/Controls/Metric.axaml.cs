using Avalonia.Controls;

namespace Monitor.Views.Controls;

public partial class Metric : Viewbox
{
    private bool showCenterValue;

    /// <summary>
    /// Single-cell (S tier) mode: the metric icon makes the ring cluttered at a
    /// ~50px diameter — replace it with the percentage in the center instead.
    /// </summary>
    public bool ShowCenterValue
    {
        get => showCenterValue;
        set
        {
            showCenterValue = value;
            CenterValue.IsVisible = value;
            Icon.IsVisible = !value;
        }
    }

    public Metric()
    {
        InitializeComponent();
        // StrokeDashOffset animation causing memory leaks
        // https://github.com/AvaloniaUI/Avalonia/issues/16973
        // 
        // ProgressBar.Transitions = new Transitions
        // {
        //     new DoubleTransition { Property = Shape.StrokeDashOffsetProperty, Duration = TimeSpan.FromMilliseconds(300) }
        // };
    }
}
