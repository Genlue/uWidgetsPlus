using Avalonia.Collections;
using Avalonia.Media;

namespace Weather.ViewModels;

public record MetricViewModel(double Min, double Max, double Value, StreamGeometry? Icon)
{
    public int? DisplayMin => Icon != null ? null : (int)Math.Round(Min);
    public int? DisplayMax => Icon != null ? null : (int)Math.Round(Max);
    public int? DisplayValue => (int)Math.Round(Value);
    public int FontSize => DisplayValue.ToString()?.Length > 2
        ? 50 * 2 / DisplayValue.ToString()?.Length ?? 2
        : 50;

    public double Progress => (Max - Min) <= 0 ? 0 : Math.Clamp((Value - Min) / (Max - Min), 0, 1);

    public bool IsProgressVisible => Progress > 0.001;

    public AvaloniaList<double> StrokeDashArray =>
        new AvaloniaList<double> { Math.Max(0.0001, Progress * 21.0), 100 };

    public double StrokeDashOffset => 0;
}