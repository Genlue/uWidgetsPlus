using System;
using System.Collections.Generic;
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
    private readonly ILayoutProvider layoutProvider;
    private readonly DisplayMonitorService displayMonitor;
    private readonly Func<UserControl> userControl;
    private readonly Func<Settings> settingsWindow;
    private readonly Func<EditWidget>? editWidgetWindow;
    private (int Columns, int Rows)? manualSpan;
    private readonly bool isFrameless;

    public bool IsFrameless => isFrameless;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public Widget(IAppSettingsProvider appSettingsProvider, IWidgetLayoutProvider widgetLayoutProvider, 
        IGridService<Widget> gridService, ILayoutProvider layoutProvider, DisplayMonitorService displayMonitor,
        Func<UserControl> userControl, Func<Settings> settingsWindow, 
        Func<EditWidget>? editWidgetWindow = null)
    {
        this.settingsWindow = settingsWindow;
        this.editWidgetWindow = editWidgetWindow;
        this.widgetLayoutProvider = widgetLayoutProvider;
        this.userControl = userControl;
        this.appSettingsProvider = appSettingsProvider;
        this.gridService = gridService;
        this.layoutProvider = layoutProvider;
        this.displayMonitor = displayMonitor;
        
        InitializeComponent();

        var control = userControl();
        isFrameless = control is IFramelessWidget;
        if (isFrameless)
            control.Classes.Add("Frameless");
        ContentPresenter.Content = control;
        
        // The native transparency level is a LOCAL value, not a style: a runtime
        // surface switch then deterministically reconfigures the existing window
        // (style-based switching could leave the OS blur backdrop behind, making
        // a translucent solid card look like frosted glass).
        ApplyTransparencyHint();

        Height = widgetLayoutProvider.Get().Height;
        Width = widgetLayoutProvider.Get().Width;
        Title = $"{widgetLayoutProvider.Get().Type} {widgetLayoutProvider.Get().SubType}";
        DataContext = this;
        
        SetMinMaxSize(this.appSettingsProvider.Get().Layout.LockSize);
        RenderOptions.SetTextRenderingMode(this, TextRenderingMode.Antialias);
        UpdateContentSize();
        
        // Manual grid: size is driven by the grid (span derived from the stored pixel size).
        if (appSettingsProvider.Get().Layout.GridMode == GridMode.Manual)
        {
            manualSpan = GetSpan();
            gridService.SetSize(this, manualSpan.Value.Columns, manualSpan.Value.Rows);
        }
        
        Activated += OnActivated;
        Opened += OnOpened;
        Resized += OnResized;
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        widgetLayoutProvider.DataChanged += OnWidgetLayoutUpdated;
        appSettingsProvider.DataChanged += OnAppSettingsUpdated;
        layoutProvider.DataChanged += OnLayoutDataUpdated;
        Unloaded += OnUnloaded;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        // The native window exists now — size the card and clip the OS acrylic backdrop.
        displayMonitor.Attach(this);
        ApplyTransparencyHint();
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
        ApplyPosition();

        // Manual grid: snap to the nearest cell on every activation (also covers
        // widget positions stored before the grid mode was switched on).
        if (appSettingsProvider.Get().Layout.GridMode == GridMode.Manual)
            gridService.SnapPosition(this);

        Scale();
        ApplyWidgetRegion();
        InteropService.RemoveWindowFromAltTab(this);
    }

    /// <summary>
    /// Place the window from the stored position. The legacy "primary" entry keeps
    /// absolute coordinates (v1 layout format); per-screen entries store positions
    /// relative to that screen's working-area origin, so re-arranging the desktop
    /// (or replugging at the same spot) restores widgets on the right screen.
    /// </summary>
    private void ApplyPosition()
    {
        var settings = widgetLayoutProvider.Get();

        if (widgetLayoutProvider.ScreenId == ScreensLayout.LegacyPrimaryId)
        {
            Position = new PixelPoint(settings.X, settings.Y);
            return;
        }

        var workingArea = displayMonitor.FindByConfigId(widgetLayoutProvider.ScreenId)?.Screen.WorkingArea;
        Position = new PixelPoint((workingArea?.X ?? 0) + settings.X, (workingArea?.Y ?? 0) + settings.Y);
    }

    public bool ShowEditButton => editWidgetWindow != null;
    public string Edit => $"{Locale.Widget_Edit} \"{widgetLayoutProvider.Get().Type}\"";

    /// <summary>
    /// Adaptively scale corner radius for widgets of different sizes.
    /// 1x1 small widgets scale to ~55% (iOS app icon style, balanced curvature).
    /// 1xN or Nx1 strip widgets scale to ~72% (prevents excessive rounding on the short edge).
    /// Standard and large widgets (2x2, 4x2, etc.) keep the full configured radius.
    /// </summary>
    private double ResolveEffectiveRadius(double baseRadius)
    {
        if (baseRadius <= 0) return 0;

        int cols = 2, rows = 2;
        if (pendingSpan.HasValue)
        {
            cols = pendingSpan.Value.Columns;
            rows = pendingSpan.Value.Rows;
        }
        else if (manualSpan.HasValue)
        {
            cols = manualSpan.Value.Columns;
            rows = manualSpan.Value.Rows;
        }
        else
        {
            try
            {
                (cols, rows) = GetSpan();
            }
            catch { }
        }

        var width = ClientSize.Width > 0 ? ClientSize.Width : Width;
        var height = ClientSize.Height > 0 ? ClientSize.Height : Height;
        var margin = WidgetMargin.Left;
        var cardW = Math.Max(1, width - 2 * margin);
        var cardH = Math.Max(1, height - 2 * margin);
        var minSide = Math.Min(cardW, cardH);

        // 1x1 small widget (single file, icon tile, or <= 90px square)
        if ((cols <= 1 && rows <= 1) || minSide <= 90)
        {
            return Math.Max(4, Math.Round(baseRadius * 0.55));
        }

        // 1xN or Nx1 strip widget (e.g. 2x1, 4x1, 1x2, 1x4, or short edge <= 125px)
        if (cols <= 1 || rows <= 1 || minSide <= 125)
        {
            return Math.Max(6, Math.Round(baseRadius * 0.72));
        }

        return baseRadius;
    }

    public CornerRadius Radius => (isFrameless || appSettingsProvider.Get().Theme.UseNativeFrame)
        ? new(0)
        : new(ResolveEffectiveRadius(appSettingsProvider.Get().Dimensions.Radius) / (Screens.ScreenFromWindow(this)?.Scaling ?? 1.0));

    public Theme GlassMaterial => appSettingsProvider.Get().Theme;
    public bool IsLiquidGlass => !isFrameless && GlassMaterial.IsLiquidGlass;

    /// <summary>
    /// True when the card should render the outline highlight ring: glass surface
    /// with the outline width &gt; 0, no native frame.
    /// </summary>
    private bool IsOutlined =>
        !isFrameless
        && appSettingsProvider.Get().Theme.IsGlass
        && appSettingsProvider.Get().Theme.OutlineWidth > 0
        && !appSettingsProvider.Get().Theme.UseNativeFrame;

    /// <summary>Highlight ring thickness (DIPs), 0 when the surface is not outlined glass.</summary>
    public Thickness WidgetOutlineThickness
    {
        get
        {
            if (!IsOutlined) return new Thickness(0);
            var width = Math.Clamp(appSettingsProvider.Get().Theme.OutlineWidth, 0, 6);
            return new Thickness(width);
        }
    }

    /// <summary>
    /// Highlight ring brush for outlined glass. The ring is strongest at the
    /// top-left and bottom-right corners and fades linearly along every edge to
    /// nothing at the top-right and bottom-left corners (无→最浓 gradient over
    /// the whole edge, not just a short notch near the corner). Built as a conic
    /// gradient whose sweep starts at the actual top-left corner, so the fades
    /// follow the real corners for any aspect ratio.
    /// </summary>
    public IBrush? WidgetOutlineBrush => IsOutlined ? BuildOutlineBrush() : null;

    private ConicGradientBrush BuildOutlineBrush()
    {
        var width = Math.Max(1, (ClientSize.Width > 0 ? ClientSize.Width : Width) - 2 * WidgetMargin.Left);
        var height = Math.Max(1, (ClientSize.Height > 0 ? ClientSize.Height : Height) - 2 * WidgetMargin.Top);

        var theme = appSettingsProvider.Get().Theme;
        var cornerAngle = Math.Atan2(width / 2.0, height / 2.0) * 180.0 / Math.PI;   // top-right corner direction
        var startAngle = 360.0 - cornerAngle;                                        // top-left corner direction
        var color = Color.TryParse(theme.EffectiveOutlineColor, out var parsed)
            ? parsed
            : Color.Parse(uWidgets.Core.Models.Settings.Theme.DefaultOutlineColor);
        var clear = Colors.Transparent;

        // Conic gradients: 0° = above center (top), clockwise (CSS convention);
        // the Angle property rotates offset 0 to the given direction (here: top-left).
        // Offsets are 0..1 fractions of the full 360° sweep. Segment widths in angle:
        //   TL→TR = 2·cornerAngle (top edge), TR→BR = 180−2·cornerAngle (right edge),
        //   BR→BL = 2·cornerAngle (bottom edge), BL→TL = 180−2·cornerAngle (left edge).
        return new ConicGradientBrush
        {
            Angle = startAngle,
            Center = RelativePoint.Center,
            GradientStops =
            {
                new GradientStop(color, 0),
                new GradientStop(clear, 2 * cornerAngle / 360.0),
                new GradientStop(color, 0.5),
                new GradientStop(clear, 0.5 + 2 * cornerAngle / 360.0),
                new GradientStop(color, 1.0)
            }
        };
    }

    /// <summary>
    /// Margin between the widget content and the grid lines (manual grid mode).
    /// </summary>
    public Thickness WidgetMargin => isFrameless ? new Thickness(0) :
        (appSettingsProvider.Get().Layout.GridMode == GridMode.Manual
            ? new Thickness(appSettingsProvider.Get().Dimensions.Margin)
            : new Thickness(0));

    public IBrush WidgetCardBackground => isFrameless
        ? Brushes.Transparent
        : (this.TryFindResource("WidgetBackground", out var res) && res is IBrush brush ? brush : Brushes.Transparent);

    /// <summary>
    /// Context-menu size section title: "Grid size" in manual mode, with the
    /// current cell span (S/M/L presets are 1×1 / 2×1 / 2×2; anything else is
    /// a custom size from the dialog).
    /// </summary>
    public string SizeMenuTitle
    {
        get
        {
            var baseTitle = appSettingsProvider.Get().Layout.GridMode == GridMode.Manual
                ? Locale.Widget_Size_Grid
                : Locale.Widget_Size;
            var (columns, rows) = CurrentSpan;
            return $"{baseTitle} · {columns}×{rows}";
        }
    }

    /// <summary>The widget's current cell span for the active grid mode.</summary>
    public (int Columns, int Rows) CurrentSpan
    {
        get
        {
            // During a preset resize animation the pixel size carries intermediate
            // values — report the target span until it settles.
            if (pendingSpan is { } target) return target;

            if (appSettingsProvider.Get().Layout.GridMode == GridMode.Manual)
            {
                manualSpan ??= GetSpan();
                return manualSpan.Value;
            }

            var dimensions = appSettingsProvider.Get().Dimensions;
            var unit = dimensions.Size + dimensions.Margin;
            return (
                Math.Max(1, (int) Math.Round((Width + dimensions.Margin) / unit)),
                Math.Max(1, (int) Math.Round((Height + dimensions.Margin) / unit)));
        }
    }

    /// <summary>Target span of an in-flight preset resize animation (null when idle).</summary>
    private (int Columns, int Rows)? pendingSpan;

    public SystemDecorations WidgetSystemDecorations => appSettingsProvider.Get().Theme.UseNativeFrame
        ? SystemDecorations.BorderOnly
        : SystemDecorations.None;

    public bool ToolTipVisible => !appSettingsProvider.Get().Theme.UseNativeFrame
                                  && !appSettingsProvider.Get().Layout.LockSize
                                  && appSettingsProvider.Get().Layout.GridMode != GridMode.Manual;
    public bool WidgetExtendClientArea => appSettingsProvider.Get().Theme.UseNativeFrame;
    public void EditWidget() => editWidgetWindow?.Invoke().ShowDialog(this);

    /// <summary>
    /// Context-menu stepper value: the widget's column span. Setting it resizes
    /// the widget to the new span (the 300 ms transition and the post-animation
    /// snap/lock happen inside <see cref="Resize"/>).
    /// </summary>
    public decimal? SizeColumnsValue
    {
        get => CurrentSpan.Columns;
        set
        {
            var (columns, rows) = CurrentSpan;
            if (value is not { } target) return;
            var next = Math.Clamp((int) Math.Round(target), 1, 999);
            if (next == columns) return;
            _ = Resize(next, rows);
        }
    }

    /// <summary>Context-menu stepper value: the widget's row span (see <see cref="SizeColumnsValue"/>).</summary>
    public decimal? SizeRowsValue
    {
        get => CurrentSpan.Rows;
        set
        {
            var (columns, rows) = CurrentSpan;
            if (value is not { } target) return;
            var next = Math.Clamp((int) Math.Round(target), 1, 999);
            if (next == rows) return;
            _ = Resize(columns, next);
        }
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
        var contentScale = EffectiveContentScale;

        if (Math.Abs(contentScale - 1.0) < 0.001)
        {
            ContentPresenter.RenderTransform = null;
            return;
        }

        ContentPresenter.RenderTransform = new ScaleTransform(contentScale, contentScale);
        ContentPresenter.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
    }

    /// <summary>
    /// The widget's effective content scale: its own choice (context menu),
    /// then the per-screen default, then 1.0.
    /// </summary>
    private double EffectiveContentScale =>
        widgetLayoutProvider.Get().ContentScale
        ?? displayMonitor.Find(this)?.Config?.ContentScale
        ?? 1.0;

    /// <summary>Context-menu scale entry, showing the current effective ratio.</summary>
    public string ScaleMenuTitle => $"{Locale.Widget_Scale} · {EffectiveContentScale:0.##}×";

    /// <summary>Open the free-form scale input dialog for this widget.</summary>
    public void OpenScaleDialog() =>
        new ScaleDialog(EffectiveContentScale, ApplyContentScale).ShowDialog(this);

    private void ApplyContentScale(double? value)
    {
        var settings = widgetLayoutProvider.Get();
        if (settings.ContentScale == value) return;

        widgetLayoutProvider.Save(settings with { ContentScale = value });
        Scale();
        Notify(nameof(ScaleMenuTitle));
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
        if (width <= 0 || height <= 0) return;

        if (isFrameless)
        {
            Border.Width = width;
            Border.Height = height;
            Border.Margin = new Thickness(0);
            Border.BorderThickness = new Thickness(0);
            Border.CornerRadius = new CornerRadius(0);
            Border.BorderBrush = null;
            Border.Background = Brushes.Transparent;

            ContentPresenter.Width = width;
            ContentPresenter.Height = height;
            ContentPresenter.Clip = null;
            return;
        }

        var margin = WidgetMargin.Left;
        var cardW = Math.Max(1, width - 2 * margin);
        var cardH = Math.Max(1, height - 2 * margin);

        Border.Width = cardW;
        Border.Height = cardH;

        var outline = WidgetOutlineThickness;
        var innerW = Math.Max(1, cardW - outline.Left - outline.Right);
        var innerH = Math.Max(1, cardH - outline.Top - outline.Bottom);

        ContentPresenter.Width = innerW;
        ContentPresenter.Height = innerH;

        Notify(nameof(Radius));
        var r = Radius.TopLeft;
        var innerR = Math.Max(0, r - outline.Left);
        ContentPresenter.Clip = new RectangleGeometry(new Rect(0, 0, innerW, innerH), innerR, innerR);

        // Outlined glass: the corner-fade notches follow the card aspect ratio.
        Notify(nameof(WidgetOutlineThickness));
        Notify(nameof(WidgetOutlineBrush));
    }

    /// <summary>
    /// Clip the native window (and therefore the OS-level acrylic backdrop) to the
    /// card rectangle: the grid cell inset by the widget margin, with the corner
    /// radius. Outside the card the window becomes fully transparent and
    /// click-through; the frosted glass never covers the empty grid cells.
    /// </summary>
    private void ApplyWidgetRegion()
    {
        // Frameless widgets manage their own window region and glyph clipping;
        // never clear or overwrite their region externally.
        if (isFrameless) return;

        // Native frame: no custom clipping.
        if (appSettingsProvider.Get().Theme.UseNativeFrame)
        {
            InteropService.ClearWidgetRegion(this);
            return;
        }

        // Only frosted glass (acrylic) needs native window clipping because OS DWM
        // applies acrylic blur to the entire HWND.
        // Liquid glass and solid surfaces do NOT use native blur; their transparent
        // margins and anti-aliased rounded corners composite via 32-bit per-pixel alpha,
        // so applying a 1-bit GDI region truncates the anti-aliased curved edge and creates jaggedness.
        if (!appSettingsProvider.Get().Theme.UsesNativeBlur)
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

        InteropService.SetWidgetRegion(
            this,
            margin,
            margin,
            cardWidth,
            cardHeight,
            (int) Math.Round(ResolveEffectiveRadius(appSettingsProvider.Get().Dimensions.Radius)));
    }

    private void OnAppSettingsUpdated(object sender, AppSettings? oldData, AppSettings newData)
    {
        if (oldData?.Layout.LockSize != newData.Layout.LockSize)
            SetMinMaxSize(newData.Layout.LockSize);

        // Grid mode / grid geometry changed → re-apply the grid-driven size and
        // re-snap the position while keeping the cell span.
        if (oldData?.Layout.GridMode != newData.Layout.GridMode || oldData?.Grid != newData.Grid)
        {
            if (newData.Layout.GridMode == GridMode.Manual)
            {
                manualSpan ??= GetSpan();
                var (columns, rows) = manualSpan.Value;

                SetMinMaxSize(false);
                gridService.SetSize(this, columns, rows);
                SetMinMaxSize(true);
                AfterMove();
                AfterResize();
            }
            else
            {
                manualSpan = null;
            }
        }

        if (oldData?.Dimensions != newData.Dimensions)
        {
            AfterMove();
            AfterResize();
        }

        // Make bound properties reactive so style changes apply immediately
        // (margin from grid lines, corner radius, context menu, tooltip and the
        // outlined-glass highlight ring…).
        if (oldData?.Dimensions != newData.Dimensions || oldData?.Layout.GridMode != newData.Layout.GridMode
            || oldData?.Layout.LockSize != newData.Layout.LockSize || oldData?.Theme != newData.Theme)
        {
            // Surface switch (毛玻璃↔纯色): reconfigure the native transparency.
            if (oldData?.Theme?.UsesNativeBlur != newData.Theme.UsesNativeBlur)
                ApplyTransparencyHint();
            Notify(nameof(GlassMaterial));
            Notify(nameof(IsLiquidGlass));
            Notify(nameof(WidgetMargin));
            Notify(nameof(WidgetCardBackground));
            Notify(nameof(Radius));
            Notify(nameof(SizeMenuTitle));
            Notify(nameof(ToolTipVisible));
            Notify(nameof(WidgetOutlineThickness));
            Notify(nameof(WidgetOutlineBrush));
            UpdateContentSize();
            ApplyWidgetRegion();
        }
    }

    /// <summary>
    /// Native window transparency: <see cref="WindowTransparencyLevel.AcrylicBlur"/>
    /// for glass surfaces (OS-level live blur, per-frame desktop sampling) and
    /// <see cref="WindowTransparencyLevel.Transparent"/> for solid surfaces
    /// (per-pixel alpha, no blur — the opacity slider blends the card with the
    /// desktop without a frosted look). Applied as a local value so runtime
    /// surface switches always reconfigure the existing native window.
    /// </summary>
    private void ApplyTransparencyHint()
    {
        if (isFrameless) return;

        TransparencyLevelHint = appSettingsProvider.Get().Theme.UsesNativeBlur
            ? [WindowTransparencyLevel.AcrylicBlur]
            : [WindowTransparencyLevel.Transparent];
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
        layoutProvider.DataChanged -= OnLayoutDataUpdated;
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

        if (oldLayout?.ContentScale != newLayout.ContentScale)
        {
            Scale();
            Notify(nameof(ScaleMenuTitle));
        }
    }

    public void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (appSettingsProvider.Get().Layout.LockPosition) return;
        
        // In manual grid mode, ignore drag if the click landed in the outer grid margin
        if (appSettingsProvider.Get().Layout.GridMode == GridMode.Manual)
        {
            var margin = WidgetMargin;
            var p = e.GetPosition(this);
            var card = new Rect(margin.Left, margin.Top,
                Math.Max(0, (ClientSize.Width > 0 ? ClientSize.Width : Width) - margin.Left - margin.Right),
                Math.Max(0, (ClientSize.Height > 0 ? ClientSize.Height : Height) - margin.Top - margin.Bottom));
            if (!card.Contains(p)) return;
        }

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
        
        // Cross-screen move: capture the span against the ORIGINAL screen before
        // ownership transfers (after the transfer the stored size would be read
        // against the new cell and become ambiguous, e.g. 2×2 → 1×1).
        var owning = displayMonitor.FindByConfigId(widgetLayoutProvider.ScreenId);
        var current = displayMonitor.Find(this);
        var movedToAnotherScreen = current != null && owning != null && owning.Screen.Bounds != current.Screen.Bounds;
        var span = movedToAnotherScreen ? (manualSpan ?? ResolveSpanFor(owning)) : (Columns: 1, Rows: 1);
        
        TransferOwnership();
        
        if (movedToAnotherScreen && appSettings.Layout.GridMode == GridMode.Manual)
        {
            SetMinMaxSize(false);
            gridService.SetSize(this, span.Columns, span.Rows);
            SetMinMaxSize(true);
        }
        
        widgetLayoutProvider.Save(StorePosition(widgetLayoutProvider.Get()));
    }

    /// <summary>
    /// If the widget was dragged onto another screen, transfer its entry to that
    /// screen's configuration (its layout entry moves, positions become relative
    /// to the new screen's working area, and the widget's <see cref="IWidgetLayoutProvider.ScreenId"/>
    /// is rebound so every subsequent save lands in the right screen).
    /// </summary>
    private void TransferOwnership()
    {
        var current = displayMonitor.Find(this);
        if (current == null) return;

        var owning = displayMonitor.FindByConfigId(widgetLayoutProvider.ScreenId);
        if (owning != null && owning.Screen.Bounds == current.Screen.Bounds) return;

        var config = current.Config ?? displayMonitor.EnsureConfig(current);
        var screens = layoutProvider.Get();
        var oldConfig = screens.FindById(widgetLayoutProvider.ScreenId);
        var widget = widgetLayoutProvider.Get();

        if (oldConfig != null)
            screens = screens.WithScreen(oldConfig with
            {
                Layout = oldConfig.Layout.Where(item => item != widget).ToList()
            });

        screens = screens.UpsertScreen(config with
        {
            Layout = config.Layout.Where(item => item != widget).Concat([widget]).ToList()
        });

        layoutProvider.Save(screens);
        widgetLayoutProvider.ScreenId = config.Id;
    }

    /// <summary>
    /// Store the window position: absolute for the legacy "primary" entry (v1
    /// layout format), relative to the owning screen's working-area origin for
    /// per-screen entries — so display re-arrangement or replugging a screen in
    /// the same spot restores each widget on its own screen.
    /// </summary>
    private WidgetLayout StorePosition(WidgetLayout settings)
    {
        if (widgetLayoutProvider.ScreenId == ScreensLayout.LegacyPrimaryId)
            return settings with { X = Position.X, Y = Position.Y };

        var workingArea = displayMonitor.FindByConfigId(widgetLayoutProvider.ScreenId)?.Screen.WorkingArea;
        return settings with
        {
            X = Position.X - (workingArea?.X ?? 0),
            Y = Position.Y - (workingArea?.Y ?? 0)
        };
    }

    private async Task Resize(int columns, int rows)
    {
        // Remember the target span while the transition runs: the animated Width/
        // Height carry intermediate values, and both the snap/lock below and the
        // children's tier resolution (SizeTiers) must see the TARGET, not the
        // mid-animation size (locking to the animated value previously made the
        // resize snap back to the old span, e.g. M → stays 2×2).
        pendingSpan = (columns, rows);
        manualSpan = (columns, rows);
        SetMinMaxSize(false);
        Transitions = new Transitions
        {
            new DoubleTransition { Property = WidthProperty, Duration = TimeSpan.FromMilliseconds(300) },
            new DoubleTransition { Property = HeightProperty, Duration = TimeSpan.FromMilliseconds(300) }
        };
        gridService.SetSize(this, columns, rows);
        await Task.Delay(320);
        Transitions = null;
        AfterResize();
        SetMinMaxSize(true);
        pendingSpan = null;
    }

    private void AfterResize()
    {
        var appSettings = appSettingsProvider.Get();
        
        if (appSettings.Layout.GridMode == GridMode.Manual || appSettings.Layout.SnapSize)
            gridService.SnapSize(this);
        
        Scale();
        var settings = widgetLayoutProvider.Get();
        widgetLayoutProvider.Save(settings with { Width = (int)Width, Height = (int)Height });
        Notify(nameof(Radius));
        UpdateContentSize();
        ApplyWidgetRegion();
        Notify(nameof(SizeMenuTitle));
        // Re-sync the context-menu size steppers with the committed span (the
        // binding does not refresh itself while the menu is open).
        Notify(nameof(SizeColumnsValue));
        Notify(nameof(SizeRowsValue));
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
    /// Resolve the widget's cell span against a SPECIFIC screen's grid (used when
    /// the widget is dragged to another screen, so the span is preserved even
    /// though the cell size differs between the screens).
    /// </summary>
    private (int Columns, int Rows) ResolveSpanFor(AttachedScreen attached)
    {
        var grid = attached.Config?.Grid ?? appSettingsProvider.Get().Grid ?? uWidgets.Core.Models.Settings.Grid.Default;
        var area = attached.Screen.WorkingArea;
        var (cellPx, _, _) = GridMetrics.Resolve(grid, area.X, area.Y, area.Width, area.Height);
        var layout = widgetLayoutProvider.Get();
        var scaling = attached.Screen.Scaling;
        return (ResolveSpan(layout.Width, cellPx, scaling), ResolveSpan(layout.Height, cellPx, scaling));
    }

    /// <summary>
    /// Resolve the widget's cell span against a specific Grid configuration.
    /// </summary>
    private (int Columns, int Rows) ResolveSpanAgainstGrid(uWidgets.Core.Models.Settings.Grid grid)
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary ?? Screens.All.FirstOrDefault();
        var area = screen?.WorkingArea;
        var scaling = screen?.Scaling ?? 1.0;
        var (cellPx, _, _) = GridMetrics.Resolve(grid, area?.X ?? 0, area?.Y ?? 0, area?.Width ?? 1920, area?.Height ?? 1080);
        var layout = widgetLayoutProvider.Get();
        return (ResolveSpan(layout.Width, cellPx, scaling), ResolveSpan(layout.Height, cellPx, scaling));
    }
    
    /// <summary>
    /// Resolve a stored pixel size into a whole number of grid cells.
    /// <para>
    /// The current build always stores window sizes in DIPs, so the DIP
    /// interpretation is trusted whenever it lands close to a whole number of
    /// cells. Sizes saved by older builds were physical pixels (e.g. 575 px on a
    /// 125% display for a 5-cell span) — those are only used when the DIP reading
    /// is clearly not a whole span. Comparing both and taking the closer one used
    /// to report the wrong span whenever both readings were equidistant (5 cells
    /// on a 125% display: 460 DIP = 4.0 cells physical = 5.0 cells DIP), so the
    /// size steppers showed 4 and setting 4 was a no-op ("adjusting columns
    /// 5 to 4 does nothing").
    /// </para>
    /// </summary>
    private static int ResolveSpan(double size, double cellPx, double scaling)
    {
        var cellDip = cellPx / scaling;
        if (cellDip <= 0) return 1;

        var dip = size / cellDip;
        var dipError = Math.Abs(dip - Math.Round(dip));
        if (dipError <= 0.15) return Math.Max(1, (int) Math.Round(dip));

        var physical = size / cellPx;
        if (Math.Abs(physical - Math.Round(physical)) <= 0.05)
            return Math.Max(1, (int) Math.Round(physical));

        return Math.Max(1, (int) Math.Round(dip));
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
        // Per-screen manual grid (the screen the widget currently sits on),
        // falling back to the global grid, then the default.
        var grid = displayMonitor.Find(this)?.Config?.Grid
                   ?? appSettingsProvider.Get().Grid
                   ?? uWidgets.Core.Models.Settings.Grid.Default;
        return GridMetrics.Resolve(
            grid,
            area?.X ?? 0,
            area?.Y ?? 0,
            area?.Width ?? 1920,
            area?.Height ?? 1080);
    }

    /// <summary>
    /// React to per-screen configuration changes — grid geometry, content scale
    /// or an ownership transfer from a cross-screen drag — by re-applying the
    /// grid-driven size, the content scale and the OS clipping region.
    /// </summary>
    private void OnLayoutDataUpdated(object? sender, ScreensLayout? oldScreens, ScreensLayout newScreens)
    {
        var config = newScreens.FindById(widgetLayoutProvider.ScreenId);
        var oldConfig = oldScreens?.FindById(widgetLayoutProvider.ScreenId);
        if (config == null) return; // this widget is no longer in the layout

        if (Equals(oldConfig?.Grid, config.Grid) && Equals(oldConfig?.ContentScale, config.ContentScale))
            return;

        if (appSettingsProvider.Get().Layout.GridMode == GridMode.Manual && !Equals(oldConfig?.Grid, config.Grid))
        {
            manualSpan ??= (oldConfig?.Grid != null ? ResolveSpanAgainstGrid(oldConfig.Grid) : GetSpan());
            var (columns, rows) = manualSpan.Value;

            SetMinMaxSize(false);
            gridService.SetSize(this, columns, rows);
            SetMinMaxSize(true);
            AfterResize();

            // The grid editor just saved a NEW grid: re-snap to the new cell
            // origin immediately and persist the position (before this fix the
            // widgets only re-snapped on activation, so closing the editor with
            // "完成" appeared to do nothing).
            gridService.SnapPosition(this);
            widgetLayoutProvider.Save(StorePosition(widgetLayoutProvider.Get()));
        }

        Scale();
        Notify(nameof(ScaleMenuTitle));
        ApplyWidgetRegion();
    }
}
