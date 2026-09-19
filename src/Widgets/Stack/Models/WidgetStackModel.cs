using System.Collections.Generic;

namespace StackWidgets.Models;

/// <summary>
/// Description of a single stacked child widget.
/// </summary>
public record StackedWidgetEntry(
    string AssemblyName,
    string ViewTypeName,
    string DisplayTitle,
    string? SettingsJson = null
);

public class StackedWidgetSettingItem
{
    public StackedWidgetEntry Entry { get; set; } = null!;
    public int Index { get; set; }
    public string DisplayTitle => Entry.DisplayTitle;
    public bool HasSettings { get; set; }
}

/// <summary>
/// Stored configuration for the Widget Stack component.
/// </summary>
public record WidgetStackModel(
    List<StackedWidgetEntry> Entries,
    int SelectedIndex = 0,
    bool AllowWheelSwitch = true
)
{
    public WidgetStackModel() : this(GetDefaultEntries(), 0, true) {}

    public static List<StackedWidgetEntry> GetDefaultEntries() =>
    [
        new StackedWidgetEntry("Clock", "AnalogI", "时钟"),
        new StackedWidgetEntry("Notes", "Note", "便签")
    ];
}
