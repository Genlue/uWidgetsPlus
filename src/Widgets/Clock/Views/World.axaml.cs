using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Clock.Models;
using Clock.ViewModels;

namespace Clock.Views;

public partial class World : UserControl
{
    private readonly WorldClockViewModel viewModel;

    public World() : this(new WorldClockModel(new List<string?>())) {}

    public World(WorldClockModel worldClockModel)
    {
        viewModel = new WorldClockViewModel(worldClockModel);
        DataContext = viewModel;
        InitializeComponent();

        Item0.DataContext = viewModel.First;
        Item1.DataContext = viewModel.Second;
        Item2.DataContext = viewModel.Third;
        Item3.DataContext = viewModel.Fourth;

        SizeChanged += OnSizeChanged;
        Unloaded += OnUnloaded;
        ApplyLayout();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e) => ApplyLayout();

    private void ApplyLayout()
    {
        var (columns, rows, showCity, showCenterDigital, showNumbers) = ResolveLayout();

        Board.ColumnDefinitions = new ColumnDefinitions(columns switch
        {
            4 => "*,*,*,*",
            2 => "*,*",
            _ => "*"
        });
        Board.RowDefinitions = new RowDefinitions(rows switch
        {
            4 => "*,*,*,*",
            2 => "*,*",
            _ => "*"
        });

        (Board.Width, Board.Height) = ResolveBoardSize(columns, rows, showCity);

        var items = new[] { Item0, Item1, Item2, Item3 };
        var clocks = new[] { Clock0, Clock1, Clock2, Clock3 };
        var cities = new[] { City0, City1, City2, City3 };

        var index = 0;
        for (var row = 0; row < rows && index < items.Length; row++)
        {
            for (var col = 0; col < columns && index < items.Length; col++)
            {
                items[index].IsVisible = true;
                Grid.SetColumn(items[index], col);
                Grid.SetRow(items[index], row);
                cities[index].IsVisible = showCity;
                clocks[index].ShowNumbers = showNumbers;
                index++;
            }
        }
        for (; index < items.Length; index++) items[index].IsVisible = false;

        if (showCenterDigital)
        {
            Grid.SetColumn(CenterOverlay, 0);
            Grid.SetRow(CenterOverlay, 0);
            Grid.SetColumnSpan(CenterOverlay, columns);
            Grid.SetRowSpan(CenterOverlay, rows);
            CenterOverlay.IsVisible = true;
        }
        else
        {
            CenterOverlay.IsVisible = false;
        }
    }

    /// <summary>
    /// Choose the world-clock arrangement from the hosting widget's grid span:
    /// 2x2 -> 2x2 dials + center digital time; 4x2 -> one row of four bigger dials
    /// with city labels (like the 4x2 Monitor Multi-Dashboard); 4x4 -> 2x2 dials
    /// with city labels; everything else adapts by span/aspect ratio.
    /// </summary>
    private (int Columns, int Rows, bool ShowCityNames, bool ShowCenterDigital, bool ShowNumbers) ResolveLayout()
    {
        var span = FindHostSpan();
        if (span is { } s)
        {
            if (s.Columns == 1 && s.Rows == 1) return (1, 1, false, false, false);
            if (s.Columns >= 3 && s.Rows >= 3) return (2, 2, true, false, true);
            if (s.Columns >= 3 && s.Rows == 2) return (4, 1, true, false, true);
            if (s.Columns >= 3 && s.Rows == 1) return (4, 1, false, false, true);
            if (s.Columns == 1 && s.Rows >= 3) return (1, 4, true, false, true);
            if (s.Columns == 1 && s.Rows == 2) return (2, 2, false, true, false);
            if (s.Columns == 2 && s.Rows == 1) return (4, 1, false, false, false);
            if (s.Columns == 2 && s.Rows == 2) return (2, 2, false, true, false);
        }

        // No widget host (gallery preview): coarse aspect-ratio fallback.
        var size = Bounds.Size;
        if (size.Width > 0 && size.Width > size.Height * 1.8)
            return (4, 1, size.Height >= 100, false, true);
        if (size.Height > 0 && size.Height > size.Width * 1.8)
            return (1, 4, size.Width >= 100, false, true);
        return (2, 2, false, true, false);
    }

    private static (double Width, double Height) ResolveBoardSize(int columns, int rows, bool showCity)
    {
        if (columns == 4) return showCity ? (320, 160) : (320, 90);
        if (rows == 4) return showCity ? (120, 360) : (90, 360);
        if (columns == 1 && rows == 1) return (200, 200);
        return showCity ? (220, 220) : (200, 200);
    }

    private (int Columns, int Rows)? FindHostSpan()
    {
        for (var node = this.GetVisualParent(); node != null; node = node.GetVisualParent())
            if (node is uWidgets.Views.Widget widget)
                return widget.CurrentSpan;
        return null;
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        viewModel.Dispose();
        SizeChanged -= OnSizeChanged;
        Unloaded -= OnUnloaded;
    }
}
