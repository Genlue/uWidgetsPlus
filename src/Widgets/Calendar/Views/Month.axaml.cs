using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Calendar.Models;
using Calendar.ViewModels;
using uWidgets.Services;

namespace Calendar.Views;

public partial class Month : UserControl
{
    private readonly MonthCalendarModel monthCalendarModel;

    private readonly MonthCalendarViewModel viewModel;

    public Month() : this(new MonthCalendarModel(DayOfWeek.Monday)) {}
    
    public Month(MonthCalendarModel monthCalendarModel)
    {
        this.monthCalendarModel = monthCalendarModel;
        viewModel = new MonthCalendarViewModel(monthCalendarModel);
        DataContext = viewModel;
        Loaded += (_, _) => viewModel.Start();
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
        InitializeComponent();
        UpdateTodayBrush();
        if (Application.Current != null)
            Application.Current.ActualThemeVariantChanged += OnActualThemeVariantChanged;
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        viewModel.Stop();
    }

    private void OnActualThemeVariantChanged(object? sender, System.EventArgs e) => UpdateTodayBrush();

    /// <summary>
    /// True when the today-marker ellipse should use a fixed color instead of the
    /// (theme-reactive) accent resource. Drives the Ellipse's customColor class.
    /// </summary>
    public static readonly StyledProperty<bool> IsCustomTodayColorProperty =
        AvaloniaProperty.Register<Month, bool>(nameof(IsCustomTodayColor));

    public bool IsCustomTodayColor
    {
        get => GetValue(IsCustomTodayColorProperty);
        set => SetValue(IsCustomTodayColorProperty, value);
    }

    /// <summary>
    /// The today-marker color for custom mode, resolved against the current theme.
    /// </summary>
    public static readonly StyledProperty<IBrush?> TodayCustomBrushProperty =
        AvaloniaProperty.Register<Month, IBrush?>(nameof(TodayCustomBrush));

    public IBrush? TodayCustomBrush
    {
        get => GetValue(TodayCustomBrushProperty);
        set => SetValue(TodayCustomBrushProperty, value);
    }

    private void UpdateTodayBrush()
    {
        if (monthCalendarModel.TodayColorMode != "Custom")
        {
            IsCustomTodayColor = false;
            TodayCustomBrush = null;
            return;
        }

        var dark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        var hex = dark ? monthCalendarModel.TodayColorDark : monthCalendarModel.TodayColorLight;

        IBrush? brush = null;
        if (!string.IsNullOrWhiteSpace(hex))
        {
            try { brush = new SolidColorBrush(Color.Parse(hex)); }
            catch { brush = null; }
        }

        IsCustomTodayColor = brush != null;
        TodayCustomBrush = brush;
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
