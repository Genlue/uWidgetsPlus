namespace uWidgets.Core.Models.Settings;

/// <summary>
/// Grid layout mode.
/// </summary>
public enum GridMode
{
    /// <summary>
    /// Custom manual grid (default): the user divides the desktop into an m x n grid of
    /// square cells (<see cref="Grid"/>); widgets snap to cells and adopt the
    /// cell size (or a whole multiple of it) instead of being freely resizable.
    /// <para><c>Manual</c> is the default — the app starts with the grid system active.</para>
    /// </summary>
    Manual = 0,

    /// <summary>
    /// Virtual grid (official behavior): widgets are sized and positioned
    /// in multiples of a virtual grid unit (<see cref="Dimensions.Size"/>).
    /// </summary>
    Virtual = 1,

    /// <summary>
    /// Free layout: no grid at all, widgets can be positioned and sized freely.
    /// </summary>
    Free = 2
}
