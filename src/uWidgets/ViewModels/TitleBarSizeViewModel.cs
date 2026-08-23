using uWidgets.Core.Models.Settings;

namespace uWidgets.ViewModels;

public record TitleBarSizeViewModel(string Name, double Value)
{
    /// <summary>
    /// The traffic light size options (diameter in DIPs). 12px is the macOS
    /// standard; the first option is the effective default.
    /// </summary>
    public static TitleBarSizeViewModel[] Options { get; } =
    [
        new($"{AppSettings.DefaultTitleBarSize:0}px", AppSettings.DefaultTitleBarSize),
        new("12px", 12),
        new("16px", 16),
        new("18px", 18),
        new("20px", 20)
    ];
}
