using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Calendar.Models;
using Calendar.ViewModels;
using uWidgets.Services;

namespace Calendar.Views;

public partial class Month : UserControl
{
    public Month() : this(new MonthCalendarModel(DayOfWeek.Monday)) {}
    
    public Month(MonthCalendarModel monthCalendarModel)
    {
        DataContext = new MonthCalendarViewModel(monthCalendarModel);
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
        InitializeComponent();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        SizeChanged -= OnSizeChanged;
        ((MonthCalendarViewModel)DataContext!).Dispose();
    }

    public static readonly StyledProperty<double> TextSizeProperty = 
        AvaloniaProperty.Register<Month, double>(nameof(TextSize), 12);

    public double TextSize
    {
        get => GetValue(TextSizeProperty);
        set => SetValue(TextSizeProperty, value);
    }
    
    public static readonly StyledProperty<Thickness> MonthMarginProperty = 
        AvaloniaProperty.Register<Month, Thickness>(nameof(MonthMargin));
    
    public Thickness MonthMargin
    {
        get => GetValue(MonthMarginProperty);
        set => SetValue(MonthMarginProperty, value);
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var size = e.NewSize;
        var tier = SizeTiers.ResolveTier(this, size);

        // 1×1 (Cell): a whole month is illegible — show today only.
        // A short wide custom card (2×1 …): only one week fits the height — show
        // the current week as a single row. Everything else (2×2 / 4×2 / 4×4 and
        // big customs): the full month grid, which scales with TextSize.
        var cell = tier == WidgetTier.Cell;
        var weekRow = !cell && size.Height <= 160 && size.AspectRatio >= 1.5;

        FullMonth.IsVisible = !cell && !weekRow;
        WeekRow.IsVisible = weekRow;
        TodayOnly.IsVisible = cell;

        if (cell) return;

        if (weekRow)
        {
            // Weekday header row: the day cells scale themselves (Viewboxes);
            // the header shares the row height, so size it against half the card.
            TextSize = size.Height * 0.22;
            MonthMargin = new Thickness();
            return;
        }

        var fontSize = Math.Min(size.Width, size.Height) / 12;
        var margin = Math.Min(size.Width, size.Height) / 30;

        TextSize = fontSize;
        MonthMargin = new Thickness(-margin, 0,  0,  0);
    }
}
