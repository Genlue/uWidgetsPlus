using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models.Settings;
using uWidgets.Locales;
using uWidgets.Services;
using GridSettings = uWidgets.Core.Models.Settings.Grid;

namespace uWidgets.Views;

/// <summary>
/// Full-screen editor for the manual grid (<see cref="GridMode.Manual"/>).
/// <para>
/// · Covers the whole primary screen (DPI-correct); the highlighted grid area shows the
///   currently saved grid and can be dragged to move the whole grid on the desktop;<br/>
/// · Right-click the grid area to open the parameters panel: rows / columns / cell size,
///   X-centering button and fine X/Y nudge buttons;<br/>
/// · Every change is saved immediately and re-loaded on the next edit session.
/// </para>
/// </summary>
public partial class GridEditor : Window
{
    private readonly IAppSettingsProvider appSettingsProvider;
    private bool dragging;
    private Point dragStart;
    private double startLeft;
    private double startTop;

    public GridEditor(IAppSettingsProvider appSettingsProvider)
    {
        this.appSettingsProvider = appSettingsProvider;
        InitializeComponent();

        Opened += OnOpened;
        KeyDown += OnKeyDown;
        Closed += OnClosed;
        ColumnsInput.ValueChanged += OnCellParameterChanged;
        RowsInput.ValueChanged += OnCellParameterChanged;
        CellInput.ValueChanged += OnCellParameterChanged;

        HintText.Text = Locale.Settings_Advanced_GridEditorHint;
        SaveButton.Content = Locale.Settings_Advanced_GridEditorSave;
        CenterButton.Content = Locale.Settings_Advanced_GridEditorCenterX;
        NudgeLeftButton.Content = Locale.Settings_Advanced_GridEditorNudgeLeft;
        NudgeRightButton.Content = Locale.Settings_Advanced_GridEditorNudgeRight;
        NudgeUpButton.Content = Locale.Settings_Advanced_GridEditorNudgeUp;
        NudgeDownButton.Content = Locale.Settings_Advanced_GridEditorNudgeDown;

        // Full screen, DPI-correct: geometry is set BEFORE the window is shown so
        // the editor opens as a full-screen window every time (position in physical
        // pixels, size in DIPs = physical / scaling).
        var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
        if (screen != null)
        {
            Position = new PixelPoint(screen.Bounds.X, screen.Bounds.Y);
            Width = screen.Bounds.Width / Scaling;
            Height = screen.Bounds.Height / Scaling;
        }
        else
        {
            Width = 1920;
            Height = 1080;
        }
    }

    /// <summary>
    /// DPI scale of the primary screen (window and canvas work in DIPs).
    /// </summary>
    private double Scaling => Screens.Primary?.Scaling ?? 1.0;

    private void OnOpened(object? sender, EventArgs e)
    {
        // Keep the window pinned to the screen on every activation.
        if (Screens.Primary is { } screen)
            Position = new PixelPoint(screen.Bounds.X, screen.Bounds.Y);

        ApplyGrid();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        KeyDown -= OnKeyDown;
        Closed -= OnClosed;
        ColumnsInput.ValueChanged -= OnCellParameterChanged;
        RowsInput.ValueChanged -= OnCellParameterChanged;
        CellInput.ValueChanged -= OnCellParameterChanged;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        SaveGeometry();
        Close();
    }

    // ---------- Grid area drag (move the whole grid) ----------

    private void OnGridPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            openParamPanel();
            e.Handled = true;
            return;
        }

        dragging = true;
        dragStart = e.GetPosition(GridCanvas);
        startLeft = Canvas.GetLeft(GridVisual);
        startTop = Canvas.GetTop(GridVisual);
        e.Pointer.Capture(GridVisual);
        e.Handled = true;
    }

    private void OnGridMoved(object? sender, PointerEventArgs e)
    {
        if (!dragging) return;

        var position = e.GetPosition(GridCanvas);
        var maxLeft = Math.Max(GridCanvas.Bounds.Width - GridVisual.Bounds.Width, 0);
        var maxTop = Math.Max(GridCanvas.Bounds.Height - GridVisual.Bounds.Height, 0);

        Canvas.SetLeft(GridVisual, Math.Clamp(startLeft + position.X - dragStart.X, 0, maxLeft));
        Canvas.SetTop(GridVisual, Math.Clamp(startTop + position.Y - dragStart.Y, 0, maxTop));
        UpdateInfo();
    }

    private void OnGridReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!dragging) return;
        dragging = false;
        e.Pointer.Capture(null);
        SaveGeometry();
    }

    // ---------- Parameters panel ----------

    private void openParamPanel()
    {
        var grid = appSettingsProvider.Get().Grid ?? GridSettings.Default;
        ParamPanel.IsVisible = true;
        ColumnsInput.Value = grid.Columns;
        RowsInput.Value = grid.Rows;
        CellInput.Value = (decimal) Math.Round(grid.CellPercent);
        ParamTitle.Text = Locale.Settings_Advanced_GridEdit;
    }

    private void OnCellParameterChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (!ParamPanel.IsVisible) return;
        if (ColumnsInput.Value is not { } columns || RowsInput.Value is not { } rows || CellInput.Value is not { } cell)
            return;

        var settings = appSettingsProvider.Get();
        var current = settings.Grid ?? GridSettings.Default;
        var newGrid = current with
        {
            Columns = Math.Max(1, (int) columns),
            Rows = Math.Max(1, (int) rows),
            CellPercent = Math.Clamp((double) cell, 1, 50)
        };

        if (newGrid != current)
            appSettingsProvider.Save(settings with { Grid = newGrid });

        ApplyGrid();
    }

    // ---------- X centering & nudges ----------

    private void OnCenterClicked(object? sender, RoutedEventArgs e)
    {
        var settings = appSettingsProvider.Get();
        var grid = settings.Grid ?? GridSettings.Default;
        var centeredX = Math.Clamp((100 - grid.Columns * grid.CellPercent) / 2.0, 0, 100);
        var newGrid = grid with { XPercent = centeredX };

        if (newGrid != grid)
        {
            appSettingsProvider.Save(settings with { Grid = newGrid });
            ApplyGrid();
        }
    }

    private void OnNudgeLeftClicked(object? sender, RoutedEventArgs e) => Nudge(-1, 0);
    private void OnNudgeRightClicked(object? sender, RoutedEventArgs e) => Nudge(1, 0);
    private void OnNudgeUpClicked(object? sender, RoutedEventArgs e) => Nudge(0, -1);
    private void OnNudgeDownClicked(object? sender, RoutedEventArgs e) => Nudge(0, 1);

    /// <summary>
    /// Fine-tune the grid origin by ±1% of the working area.
    /// </summary>
    private void Nudge(int dx, int dy)
    {
        var settings = appSettingsProvider.Get();
        var grid = settings.Grid ?? GridSettings.Default;
        var newGrid = grid with
        {
            XPercent = Math.Clamp(grid.XPercent + dx, 0, 100),
            YPercent = Math.Clamp(grid.YPercent + dy, 0, 100)
        };

        if (newGrid != grid)
        {
            appSettingsProvider.Save(settings with { Grid = newGrid });
            ApplyGrid();
        }
    }

    // ---------- Geometry ----------

    /// <summary>
    /// Position and size the grid visual from the saved percentage settings
    /// (all canvas coordinates in DIPs).
    /// </summary>
    private void ApplyGrid()
    {
        var settings = appSettingsProvider.Get();
        var grid = settings.Grid ?? GridSettings.Default;
        var (cellDip, xDip, yDip) = ResolveInWindow(grid);

        Canvas.SetLeft(GridVisual, xDip);
        Canvas.SetTop(GridVisual, yDip);
        GridVisual.Width = grid.Columns * cellDip;
        GridVisual.Height = grid.Rows * cellDip;
        RebuildCells(grid);
        UpdateInfo();
    }

    /// <summary>
    /// Resolve the saved grid into editor-window coordinates (DIPs).
    /// </summary>
    private (double Cell, double X, double Y) ResolveInWindow(GridSettings grid)
    {
        if (Screens.Primary is not { } screen)
            return (96, 100, 100);

        var bounds = screen.Bounds;
        var area = screen.WorkingArea;
        var (cell, gridX, gridY) = GridMetrics.Resolve(grid, area.X, area.Y, area.Width, area.Height);
        var scaling = Scaling;

        // physical → DIP
        return (cell / scaling, (gridX - bounds.X) / scaling, (gridY - bounds.Y) / scaling);
    }

    /// <summary>
    /// Save the current grid visual geometry back as percentages (of the working area).
    /// </summary>
    private void SaveGeometry()
    {
        var settings = appSettingsProvider.Get();
        var grid = settings.Grid ?? GridSettings.Default;
        if (Screens.Primary is not { } screen) return;

        var bounds = screen.Bounds;
        var area = screen.WorkingArea;
        if (area.Width <= 0 || area.Height <= 0) return;

        var scaling = Scaling;
        var cellPx = Math.Min(
            GridVisual.Bounds.Width / Math.Max(grid.Columns, 1),
            GridVisual.Bounds.Height / Math.Max(grid.Rows, 1)) * scaling;

        var newGrid = grid with
        {
            XPercent = Math.Clamp((bounds.X + Canvas.GetLeft(GridVisual) * scaling - area.X) * 100.0 / area.Width, 0, 100),
            YPercent = Math.Clamp((bounds.Y + Canvas.GetTop(GridVisual) * scaling - area.Y) * 100.0 / area.Height, 0, 100),
            CellPercent = Math.Clamp(cellPx * 100.0 / area.Width, 1, 50)
        };

        if (newGrid != grid)
            appSettingsProvider.Save(settings with { Grid = newGrid });
    }

    private void RebuildCells(GridSettings grid)
    {
        CellsGrid.Children.Clear();
        CellsGrid.RowDefinitions.Clear();
        CellsGrid.ColumnDefinitions.Clear();

        if (grid.Columns <= 0 || grid.Rows <= 0) return;

        for (var column = 0; column < grid.Columns; column++)
            CellsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var row = 0; row < grid.Rows; row++)
            CellsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));

        for (var row = 0; row < grid.Rows; row++)
        {
            for (var column = 0; column < grid.Columns; column++)
            {
                var cell = new Border
                {
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)),
                    BorderThickness = new Thickness(0.5),
                    Margin = new Thickness(0.5)
                };
                Avalonia.Controls.Grid.SetRow(cell, row);
                Avalonia.Controls.Grid.SetColumn(cell, column);
                CellsGrid.Children.Add(cell);
            }
        }
    }

    private void UpdateInfo()
    {
        var grid = appSettingsProvider.Get().Grid ?? GridSettings.Default;
        var (cellDip, _, _) = ResolveInWindow(grid);
        InfoText.Text = string.Format(Locale.Settings_Advanced_GridEditorInfo, grid.Columns, grid.Rows, (int) Math.Round(cellDip * Scaling));
    }
}
