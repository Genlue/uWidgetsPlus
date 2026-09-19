using System;
using System.Collections.Generic;

namespace uWidgets.Core.Interfaces;

public record StackWidgetIndicatorItem(int Index, string Title, bool IsActive, double Opacity);

/// <summary>
/// Implemented by container widgets (like WidgetStackView) that host a carousel/stack of
/// interchangeable child widgets and expose external pagination indicator items.
/// </summary>
public interface IStackWidget
{
    /// <summary>Indicator items for pagination.</summary>
    IReadOnlyList<StackWidgetIndicatorItem> IndicatorItems { get; }
    /// <summary>Whether a transition animation is currently running.</summary>
    bool IsTransitionActive { get; }
    /// <summary>Switch to a specific child widget index.</summary>
    void SwitchToIndex(int index);
    /// <summary>Fired when indicator items or active index change.</summary>
    event EventHandler? IndicatorItemsChanged;

    /// <summary>Whether the current active child widget has a configurable settings dialog.</summary>
    bool CanEditCurrentChild { get; }
    /// <summary>Display title of the current active child widget.</summary>
    string? CurrentChildTitle { get; }
    /// <summary>Open the settings dialog for the current active child widget.</summary>
    void EditCurrentChild(object? ownerWindow = null);
}
