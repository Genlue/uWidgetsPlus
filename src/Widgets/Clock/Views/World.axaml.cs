using Avalonia.Controls;
using Avalonia.Interactivity;
using Clock.Models;
using Clock.ViewModels;
using Clock.Views.Controls;
using uWidgets.Services;

namespace Clock.Views;

public partial class World : UserControl
{
    public World() : this(new WorldClockModel([])) {}
    
    public World(WorldClockModel worldClockModel)
    {
        DataContext = new WorldClockViewModel(worldClockModel);
        SizeChanged += OnSizeChanged;
        Unloaded += OnUnloaded;
        InitializeComponent();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var size = e.NewSize;
        var tier = SizeTiers.ResolveTier(this, size);

        // Cell (1×1): four dials are illegible — show only the primary city,
        // full-face, across the whole card. Every other span uses the aspect
        // logic below (2×2 → 2×2 quadrants, 4×2 → four in a row, 4×4 → quadrants).
        if (tier == WidgetTier.Cell)
        {
            Grid.ColumnDefinitions = new ColumnDefinitions("*");
            Grid.RowDefinitions = new RowDefinitions("*");
            ShowSingle(First);
            return;
        }

        var wide = size.AspectRatio >= 1.5;
        
        Grid.ColumnDefinitions = new ColumnDefinitions(wide ? "*,*,*,*" : "*,*");
        Grid.RowDefinitions = new RowDefinitions(wide ? "*" : "*,*");

        ShowQuadrant(First, 0, 0, wide);
        ShowQuadrant(Second, 1, wide ? 0 : 0, wide);
        ShowQuadrant(Third, wide ? 2 : 0, wide ? 0 : 1, wide);
        ShowQuadrant(Fourth, wide ? 3 : 1, wide ? 0 : 1, wide);
    }

    private void ShowSingle(AnalogWorldSingle control)
    {
        SetAllVisible(false);
        control.IsVisible = true;
        // All four cells must sit inside the single 1×1 definition — Avalonia's
        // Grid measures invisible children too and throws on out-of-range indices.
        foreach (var cell in new[] { First, Second, Third, Fourth })
        {
            Grid.SetColumn(cell, 0);
            Grid.SetRow(cell, 0);
        }
        (control.DataContext as AnalogClockViewModel)!.ShowCityName = false;
    }

    private void ShowQuadrant(AnalogWorldSingle control, int column, int row, bool showCityName)
    {
        control.IsVisible = true;
        Grid.SetColumn(control, column);
        Grid.SetRow(control, row);
        (control.DataContext as AnalogClockViewModel)!.ShowCityName = showCityName;
    }

    private void SetAllVisible(bool visible)
    {
        First.IsVisible = visible;
        Second.IsVisible = visible;
        Third.IsVisible = visible;
        Fourth.IsVisible = visible;
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        ((WorldClockViewModel)DataContext!).Dispose();
        SizeChanged -= OnSizeChanged;
        Unloaded -= OnUnloaded;
    }
}
