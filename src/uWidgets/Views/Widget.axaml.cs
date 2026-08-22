using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;
using uWidgets.Core.Models.Settings;
using uWidgets.Locales;
using uWidgets.Services;

namespace uWidgets.Views;

public partial class Widget : Window, INotifyPropertyChanged
{
    private readonly IWidgetLayoutProvider widgetLayoutProvider;
    private readonly IAppSettingsProvider appSettingsProvider;
    private readonly IGridService<Widget> gridService;
    private readonly Func<UserControl> userControl;
    private readonly Func<Settings> settingsWindow;
    private readonly Func<EditWidget>? editWidgetWindow;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public Widget(IAppSettingsProvider appSettingsProvider, IWidgetLayoutProvider widgetLayoutProvider, 
        IGridService<Widget> gridService, Func<UserControl> userControl, Func<Settings> settingsWindow, 
        Func<EditWidget>? editWidgetWindow = null)
    {
        this.settingsWindow = settingsWindow;
        this.editWidgetWindow = editWidgetWindow;
        this.widgetLayoutProvider = widgetLayoutProvider;
        this.userControl = userControl;
        this.appSettingsProvider = appSettingsProvider;
        this.gridService = gridService;
        
        InitializeComponent();
        
        Height = widgetLayoutProvider.Get().Height;
        Width = widgetLayoutProvider.Get().Width;
        Title = $"{widgetLayoutProvider.Get().Type} {widgetLayoutProvider.Get().SubType}";
        ContentPresenter.Content = userControl();
        DataContext = this;
        
        SetMinMaxSize(this.appSettingsProvider.Get().Layout.LockSize);
        RenderOptions.SetTextRenderingMode(this, TextRenderingMode.Antialias);
        UpdateContentSize();
        
        // Manual grid: size is driven by the grid (span derived from the stored pixel size).
        if (appSettingsProvider.Get().Layout.GridMode == GridMode.Manual)
        {
            var (columns, rows) = GetSpan();
            gridService.SetSize(this, columns, rows);
        }
        
        Activated += OnActivated;
        Opened += OnOpened;
        Resized += OnResized;
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        widgetLayoutProvider.DataChanged += OnWidgetLayoutUpdated;
        appSettingsProvider.DataChanged += OnAppSettingsUpdated;
        Unloaded += OnUnloaded;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        // The native window exists now — clip the acrylic backdrop to the card.
        UpdateContentSize();
        ApplyWidgetRegion();
    }

    private void OnResized(object? sender, WindowResizedEventArgs e)
    {
        UpdateContentSize();
        ApplyWidgetRegion();
        if (appSettingsProvider.Get().Theme.UseNativeFrame)
            AfterResize();
    }

    private void OnActivated(object? sender, EventArgs e)
    {
        Position = new PixelPoint(widgetLayoutProvider.Get().X, widgetLayoutProvider.Get().Y);

        // Manual grid: snap to the nearest cell on every activation (also covers
        // widget positions stored before the grid mode was switched on).
        if (appSettingsProvider.Get().Layout.GridMode == GridMode.Manual)
            gridService.SnapPosition(this);

        Scale();
        ApplyWidgetRegion();
        InteropService.RemoveWindowFromAltTab(this);
    }

    public bool ShowEditButton => editWidgetWindow != null;
    public string Edit => $"{Locale.Widget_Edit} \"{widgetLayoutProvider.Get().Type}\"";
    public CornerRadius Radius => appSettingsProvider.Get().Theme.UseNativeFrame ? new(0) : new(appSettingsProvider.Get().Dimensions.Radius / (Screens.ScreenFromWindow(this)?.Scaling ?? 1.0));
    
    /// <summary>
    /// Margin between the widget content and the grid lines (manual grid mode).
    /// </summary>
    public Thickness WidgetMargin => appSettingsProvider.Get().Layout.GridMode == GridMode.Manual
        ? new Thickness(appSettingsProvider.Get().Dimensions.Margin)
        : new Thickness(0);

    /// <summary>
    /// Context-menu size section title: "Grid size" in manual mode.
    /// </summary>
    public string SizeMenuTitle => appSettingsProvider.Get().Layout.GridMode == GridMode.Manual
        ? Locale.Widget_Size_Grid
        : Locale.Widget_Size;

    public SystemDecorations WidgetSystemDecorations => appSettingsProvider.Get().Theme.UseNativeFrame
        ? SystemDecorations.BorderOnly
        : SystemDecorations.None;

    public bool ToolTipVisible => !appSettingsProvider.Get().Theme.UseNativeFrame
                                  && !appSettingsProvider.Get().Layout.LockSize
                                  && appSettingsProvider.Get().Layout.GridMode != GridMode.Manual;
    public bool WidgetExtendClientArea => appSettingsProvider.Get().Theme.UseNativeFrame;
    public void EditWidget() => editWidgetWindow?.Invoke().ShowDialog(this);

    /// <summary>
    /// Resize the widget to an exact cell span ("columns,rows", 1×1 … 4×4).
    /// </summary>
    public void ResizeSize(string size)
    {
        var parts = size.Split(',');
        if (parts.Length != 2) return;
        if (!int.TryParse(parts[0], out var columns)) return;
        if (!int.TryParse(parts[1], out var rows)) return;
        _ = Resize(Math.Clamp(columns, 1, 4), Math.Clamp(rows, 1, 4));
    }
    public void OpenSettings() => settingsWindow.Invoke().Show();

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e) => AfterMove();

    private void Scale()
    {
        // Content scale only: the card (Border) keeps the size the grid and the
        // widget margin dictate (cell span × cell size − 2×margin). The scale is
        // a render transform on the content inside the card, centered:
        // 1.0 = the content fills the card, 0.5 = half the card (centered),
        // 2.0 = twice the card (clipped by the card's ClipToBounds).
        UpdateContentSize();
        var contentScale = appSettingsProvider.Get().Dimensions.ContentScale;

        if (Math.Abs(contentScale - 1.0) < 0.001)
        {
            ContentPresenter.RenderTransform = null;
            return;
        }

        ContentPresenter.RenderTransform = new ScaleTransform(contentScale, contentScale);
        ContentPresenter.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
    }

    /// <summary>
    /// Size the content presenter to the card's inner area (the grid cell minus the
    /// widget margin). Without an explicit size the presenter measures the widget
    /// view with an unbounded width: views whose layout depends on their width
    /// (e.g. the monitor's left-aligned ring and percentage text) collapse to their
    /// natural content width, so the whole card no longer fills the cell and the
    /// icon position shifts with the text width.
    /// </summary>
    private void UpdateContentSize()
    {
        // ClientSize is only available once the native window exists; before that
        // the window size properties carry the same value (client == window with
        // SystemDecorations.None, which is the widget's normal mode).
        var width = ClientSize.Width > 0 ? ClientSize.Width : Width;
        var height = ClientSize.Height > 0 ? ClientSize.Height : Height;
        var margin = WidgetMargin.Left;
        ContentPresenter.Width = Math.Max(1, width - 2 * margin);
        ContentPresenter.Height = Math.Max(1, height - 2 * margin);
    }

    /// <summary>
    /// Clip the native window (and therefore the OS-level acrylic backdrop) to the
    /// card rectangle: the grid cell inset by the widget margin, with the corner
    /// radius. Outside the card the window becomes fully transparent and
    /// click-through; the frosted glass never covers the empty grid cells.
    /// </summary>
    private void ApplyWidgetRegion()
    {
        // Native frame: no custom clipping (would clip the OS frame).
        if (appSettingsProvider.Get().Theme.UseNativeFrame)
        {
            InteropService.ClearWidgetRegion(this);
            return;
        }

        var scaling = Screens.ScreenFromWindow(this)?.Scaling ?? 1.0;
        var margin = (int) Math.Round(WidgetMargin.Left * scaling);
        var width = (int) Math.Round(ClientSize.Width * scaling);
        var height = (int) Math.Round(ClientSize.Height * scaling);

        var cardWidth = Math.Max(1, width - 2 * margin);
        var cardHeight = Math.Max(1, height - 2 * margin);

        // Dimensions.Radius is stored in physical pixels (converted to DIPs for
        // the Avalonia Border by the Radius property) — use it as-is here.
        InteropService.SetWidgetRegion(
            this,
            margin,
            margin,
            cardWidth,
            cardHeight,
            appSettingsProvider.Get().Dimensions.Radius);
    }

    private void OnAppSettingsUpdated(object sender, AppSettings? oldData, AppSettings newData)
    {
        if (oldData?.Layout.LockSize != newData.Layout.LockSize)
            SetMinMaxSize(newData.Layout.LockSize);

        // Grid mode / grid geometry changed → re-apply the grid-driven size and
        // re-snap the position while keeping the cell span.
        if (oldData?.Layout.GridMode != newData.Layout.GridMode || oldData?.Grid != newData.Grid)
        {
            var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary ?? Screens.All.FirstOrDefault();
            var area = screen?.WorkingArea;
            var scaling = screen?.Scaling ?? 1.0;
            var oldGrid = oldData?.Grid ?? newData.Grid;
            // Old cell size in DIPs (the previous build stored window sizes in DIPs
            // while the grid metrics are physical — divide them back).
            var oldCell = GridMetrics.Resolve(
                oldGrid,
                area?.X ?? 0, area?.Y ?? 0, area?.Width ?? 1920, area?.Height ?? 1080).Cell / scaling;
            var columns = Math.Max(1, (int) Math.Round(Width / (double) oldCell));
            var rows = Math.Max(1, (int) Math.Round(Height / (double) oldCell));

            SetMinMaxSize(false);
            gridService.SetSize(this, columns, rows);
            SetMinMaxSize(true);
            AfterMove();
            AfterResize();
        }

        if (oldData?.Dimensions != newData.Dimensions)
        {
            AfterMove();
            AfterResize();
        }

        // Make bound properties reactive so style changes apply immediately
        // (margin from grid lines, corner radius, context menu, tooltip…).
        if (oldData?.Dimensions != newData.Dimensions || oldData?.Layout.GridMode != newData.Layout.GridMode
            || oldData?.Layout.LockSize != newData.Layout.LockSize
            || oldData?.Theme.UseNativeFrame != newData.Theme.UseNativeFrame
            || oldData?.Theme != newData.Theme)
        {
            Notify(nameof(WidgetMargin));
            Notify(nameof(Radius));
            Notify(nameof(SizeMenuTitle));
            Notify(nameof(ToolTipVisible));
            ApplyWidgetRegion();
        }
    }

    private void SetMinMaxSize(bool lockSize)
    {
        if (appSettingsProvider.Get().Layout.GridMode == GridMode.Manual)
        {
            // Manual grid: size is fully grid-driven — lock to the current size
            // (unlock temporarily while the span changes via the context menu).
            if (lockSize)
            {
                MinWidth = MaxWidth = Width;
                MinHeight = MaxHeight = Height;
            }
            else
            {
                MinWidth = MinHeight = 1;
                MaxWidth = MaxHeight = double.PositiveInfinity;
            }
            return;
        }

        var size = appSettingsProvider.Get().Dimensions.Size;
        
        MinWidth = lockSize ? Width : size;
        MinHeight = lockSize ? Height : size;
        MaxWidth = lockSize ? Width : double.PositiveInfinity;
        MaxHeight = lockSize ? Height : double.PositiveInfinity;
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        PointerPressed -= OnPointerPressed;
        PointerReleased -= OnPointerReleased;
        Resized -= OnResized;
        Activated -= OnActivated;
        Opened -= OnOpened;
        Unloaded -= OnUnloaded;
        widgetLayoutProvider.DataChanged -= OnWidgetLayoutUpdated;
        appSettingsProvider.DataChanged -= OnAppSettingsUpdated;
    }

    private void OnWidgetLayoutUpdated(object? sender, WidgetLayout? oldLayout, WidgetLayout newLayout)
    {
        if (!Equals(oldLayout?.Settings, newLayout.Settings))
        {
            // Views that manage their own state (Reminders, Notes) refresh in
            // place so the editing session survives their own saves and the
            // per-widget settings dialog. Stateless views over their model are
            // recreated, which is also how they pick up external changes.
            if (ContentPresenter.Content is IWidgetSelfRefreshing selfRefreshing)
                selfRefreshing.Refresh(newLayout);
            else
                ContentPresenter.Content = userControl();
        }
    }

    public void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (appSettingsProvider.Get().Layout.LockPosition) return;
        
        ToolTip.SetIsOpen(this, false);
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed) return;
        
        BeginMoveDrag(e);
    }

    private void AfterMove()
    {
        var appSettings = appSettingsProvider.Get();
        
        // Manual grid: snapping is always enforced.
        if (appSettings.Layout.GridMode == GridMode.Manual || appSettings.Layout.SnapPosition) 
            gridService.SnapPosition(this);
        
        var settings = widgetLayoutProvider.Get();
        widgetLayoutProvider.Save(settings with { X = Position.X, Y = Position.Y });
    }

    private async Task Resize(int columns, int rows)
    {
        SetMinMaxSize(false);
        Transitions = new Transitions
        {
            new DoubleTransition { Property = WidthProperty, Duration = TimeSpan.FromMilliseconds(300) },
            new DoubleTransition { Property = HeightProperty, Duration = TimeSpan.FromMilliseconds(300) }
        };
        gridService.SetSize(this, columns, rows);
        AfterResize();
        SetMinMaxSize(true);
        await Task.Delay(300);
        Transitions = null;
    }

    private void AfterResize()
    {
        var appSettings = appSettingsProvider.Get();
        
        if (appSettings.Layout.GridMode == GridMode.Manual || appSettings.Layout.SnapSize)
            gridService.SnapSize(this);
        
        Scale();
        var settings = widgetLayoutProvider.Get();
        widgetLayoutProvider.Save(settings with { Width = (int)Width, Height = (int)Height });
    }

    public void Remove()
    {
        widgetLayoutProvider.Remove();
        Close();
    }

    private void Resize(object? sender, PointerPressedEventArgs e)
    {
        // Manual grid: widget size is grid-driven, free resizing is disabled.
        if (appSettingsProvider.Get().Layout.GridMode == GridMode.Manual) return;
        if (appSettingsProvider.Get().Layout.LockSize) return;

        CanResize = true;
        BeginResizeDrag(WindowEdge.SouthEast, e);
        AfterResize();
        e.Handled = true;
        CanResize = false;
    }

    /// <summary>
    /// The widget's cell span (columns, rows) derived from the stored pixel size.
    /// </summary>
    private (int Columns, int Rows) GetSpan()
    {
        var (cellPx, _, _) = GetGridMetrics();
        var scaling = Screens.ScreenFromWindow(this)?.Scaling ?? 1.0;
        var layout = widgetLayoutProvider.Get();
        // Window sizes are DIPs while the grid metrics are physical — convert.
        return (ResolveSpan(layout.Width, cellPx, scaling), ResolveSpan(layout.Height, cellPx, scaling));
    }

    /// <summary>
    /// Resolve a stored pixel size into a whole number of grid cells.
    /// <para>
    /// Sizes saved by older builds were physical cell counts (e.g. 230 px on a
    /// 175% display); sizes saved by the current build are DIPs (230/1.75 ≈ 131).
    /// Pick the interpretation that lands closer to a whole number of cells, so
    /// old layouts restore as 1×1 instead of drifting to 2×2 on scaled displays.
    /// </para>
    /// </summary>
    private static int ResolveSpan(double size, double cellPx, double scaling)
    {
        var cellDip = cellPx / scaling;
        if (cellDip <= 0) return 1;

        var physical = size / cellPx;
        var dip = size / cellDip;
        var span = Math.Abs(physical - Math.Round(physical)) <= Math.Abs(dip - Math.Round(dip))
            ? (int) Math.Round(physical)
            : (int) Math.Round(dip);
        return Math.Max(1, span);
    }

    /// <summary>
    /// Resolve the manual grid metrics for the current screen (fallback: primary screen).
    /// </summary>
    private (int cell, int x, int y) GetGridMetrics()
    {
        var screen = Screens.ScreenFromWindow(this)
                     ?? Screens.Primary
                     ?? Screens.All.FirstOrDefault();
        var area = screen?.WorkingArea;
        return GridMetrics.Resolve(
            appSettingsProvider.Get().Grid,
            area?.X ?? 0,
            area?.Y ?? 0,
            area?.Width ?? 1920,
            area?.Height ?? 1080);
    }
}
