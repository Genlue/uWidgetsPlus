using Avalonia.Controls;

namespace uWidgets.ViewModels;

public record WidgetPreviewViewModel(
    UserControl Control,
    string Type,
    string Subtype,
    string? Title,
    string? Subtitle,
    int DefaultColumns = 2,
    int DefaultRows = 2);