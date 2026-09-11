using System;
using Avalonia.Controls;

namespace uWidgets.ViewModels;

public record WidgetPreviewViewModel(
    UserControl Control,
    string Type,
    string Subtype,
    string? Title,
    string? Subtitle,
    int DefaultColumns = 2,
    int DefaultRows = 2)
{
    public double PreviewWidth => DefaultColumns switch
    {
        1 => 80,
        3 => 240,
        >= 4 => 328,
        _ => 160,
    };

    public double PreviewHeight => DefaultRows switch
    {
        1 => 76,
        >= 4 => 328,
        3 => 240,
        _ => 160,
    };

    public double CardWidth => Math.Max(220, PreviewWidth + 24);
}