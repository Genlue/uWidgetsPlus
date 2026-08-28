using System;
using System.Linq;
using Avalonia;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models.Settings;
using uWidgets.Views;

namespace uWidgets.Services;

public class GridService(IAppSettingsProvider appSettingsProvider, DisplayMonitorService displayMonitor) : IGridService<Widget>
{
    public void SetSize(Widget window, int columns, int rows)
    {
        var settings = appSettingsProvider.Get();

        // Manual grid mode: widget size is driven by the grid cell size (span × cell).
        // Grid metrics are physical pixels; window sizes are DIPs → convert.
        if (settings.Layout.GridMode == GridMode.Manual)
        {
            var cell = GetGridMetrics(window).cell / GetScaling(window);
            window.Width = columns * cell;
            window.Height = rows * cell;
            return;
        }

        window.Width = GetSize(columns);
        window.Height = GetSize(rows);
    }

    public void SnapSize(Widget window)
    {
        if (appSettingsProvider.Get().Layout.GridMode == GridMode.Manual)
        {
            var cell = GetGridMetrics(window).cell / GetScaling(window);
            window.Width = Math.Max(cell, (int) Math.Round(window.Width / (double) cell) * cell);
            window.Height = Math.Max(cell, (int) Math.Round(window.Height / (double) cell) * cell);
            return;
        }

        window.Width = SnapDimension(window.Width);
        window.Height = SnapDimension(window.Height);
    }

    public void SnapPosition(Widget window)
    {
        var scaling = window.Screens.ScreenFromWindow(window)?.Scaling ?? 1.0;

        if (appSettingsProvider.Get().Layout.GridMode == GridMode.Manual)
        {
            var (cell, gridX, gridY) = GetGridMetrics(window);
            var x = gridX + SnapToCell(window.Position.X - gridX, cell);
            var y = gridY + SnapToCell(window.Position.Y - gridY, cell);
            window.Position = new PixelPoint(x, y);
            return;
        }

        window.Position = new PixelPoint(
            SnapDimension(window.Position.X, scaling, true, 0),
            SnapDimension(window.Position.Y, scaling, true, 0));
    }

    /// <summary>
    /// Window scaling of the screen the widget currently sits on (window sizes are
    /// DIPs while grid metrics are physical pixels).
    /// </summary>
    private static double GetScaling(Widget window)
        => window.Screens.ScreenFromWindow(window)?.Scaling ?? 1.0;

    /// <summary>
    /// The manual grid of the screen the widget currently sits on: the per-screen
    /// configuration grid, falling back to the global <see cref="AppSettings.Grid"/>,
    /// then <see cref="Grid.Default"/>.
    /// </summary>
    private static Grid GetGrid(Widget window, IAppSettingsProvider appSettingsProvider, DisplayMonitorService displayMonitor)
    {
        var perScreen = displayMonitor.Find(window)?.Config?.Grid;
        if (perScreen != null) return perScreen;
        return appSettingsProvider.Get().Grid ?? Grid.Default;
    }

    /// <summary>
    /// Resolve the manual grid metrics (cell size in pixels, grid origin in pixels)
    /// from the percentage settings of the screen the widget currently sits on.
    /// </summary>
    private (int cell, int gridX, int gridY) GetGridMetrics(Widget window)
    {
        var grid = GetGrid(window, appSettingsProvider, displayMonitor);
        var screen = window.Screens.ScreenFromWindow(window)
                     ?? window.Screens.Primary
                     ?? window.Screens.All.FirstOrDefault();
        var area = screen?.WorkingArea;
        var (cell, gridX, gridY) = GridMetrics.Resolve(
            grid,
            area?.X ?? 0,
            area?.Y ?? 0,
            area?.Width ?? 1920,
            area?.Height ?? 1080);
        return (cell, gridX, gridY);
    }

    /// <summary>
    /// Snap an offset (physical pixels) to the nearest manual-grid cell boundary.
    /// </summary>
    private static int SnapToCell(int offset, int cellSize)
    {
        var units = (int) Math.Round(offset / (double) cellSize);
        return units * cellSize;
    }

    private int SnapDimension(double pixels, double scaling = 1.0, bool addMargin = false, int minValue = 1)
    {
        var dimensions = appSettingsProvider.Get().Dimensions;
        var size = dimensions.Size;
        var margin = dimensions.Margin;
        var units = (int) Math.Max(minValue, Math.Round(pixels / (scaling * (size + margin))));

        return GetSize(units, scaling, addMargin);
    }

    private int GetSize(int units, double scaling = 1.0, bool addMargin = false)
    {
        var dimensions = appSettingsProvider.Get().Dimensions;
        var size = dimensions.Size;
        var margin = dimensions.Margin;

        if (addMargin)
            return (int) (scaling * units * (size + margin) + scaling * margin);

        return (int) (scaling * units * (size + margin) - scaling * margin);
    }
}