using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using uWidgets.Core.Models.Settings;
using uWidgets.Locales;

namespace uWidgets.Views.Controls;

/// <summary>
/// Custom macOS-style traffic-light title bar (close / minimize / maximize)
/// for the settings window. Replicates the IconForge "红绿灯" design:
/// 36px bar, circles with 1px border and a gap of 2/3 of the diameter
/// (center distance 5/3 × diameter), glyphs hidden by default and fading in
/// on hover, brightness press feedback and grayscale + 55% opacity when the
/// window loses focus. The <see cref="Size"/> property scales the whole group
/// proportionally (12px is the macOS standard).
/// </summary>
public partial class TrafficLightTitleBar : UserControl
{
    private Window? window;
    private IDisposable? windowStateSubscription;
    private double size = AppSettings.DefaultTitleBarSize;

    public TrafficLightTitleBar()
    {
        InitializeComponent();
        ApplyGeometry();
    }

    /// <summary>
    /// Traffic light diameter in DIPs. Scales the circles, the gap, the left
    /// offset and the glyphs proportionally (12px = macOS standard).
    /// </summary>
    public double Size
    {
        get => size;
        set
        {
            size = Math.Clamp(value, 12, 24);
            ApplyGeometry();
        }
    }

    /// <summary>
    /// Proportionally scale the whole traffic-light group from the 12px base
    /// geometry: diameter, corner radius, gap (2/3 d), left offset (7/6 d) and
    /// glyph sizes (8/7.5 @ 12px). The 36px bar and the colors stay unchanged.
    /// </summary>
    private void ApplyGeometry()
    {
        var k = size / 12;
        foreach (var button in new[] { CloseButton, MinimizeButton, MaximizeButton })
        {
            button.Width = button.Height = size;
            button.CornerRadius = new CornerRadius(size / 2);
        }
        Lights.Spacing = size * (8.0 / 12);
        Lights.Margin = new Thickness(size * (14.0 / 12), 0, 0, 0);
        CloseIcon.Width = CloseIcon.Height = size * (8.0 / 12);
        MinimizeIcon.Width = MinimizeIcon.Height = size * (8.0 / 12);
        MaximizeIcon.Width = MaximizeIcon.Height = size * (7.5 / 12);
        RestoreIcon.Width = RestoreIcon.Height = size * (7.5 / 12);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (e.Root is not Window w) return;

        window = w;
        window.Activated += OnWindowActivated;
        window.Deactivated += OnWindowDeactivated;
        windowStateSubscription = window.GetObservable(Window.WindowStateProperty)
            .Subscribe(_ => UpdateMaximizeState());
        UpdateMaximizeState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (window == null) return;

        window.Activated -= OnWindowActivated;
        window.Deactivated -= OnWindowDeactivated;
        windowStateSubscription?.Dispose();
        windowStateSubscription = null;
        window = null;
    }

    private void OnWindowActivated(object? sender, EventArgs e) => Root.Classes.Remove("unfocused");

    private void OnWindowDeactivated(object? sender, EventArgs e) => Root.Classes.Add("unfocused");

    private void OnClose(object? sender, RoutedEventArgs e) => window?.Close();

    private void OnMinimize(object? sender, RoutedEventArgs e) => window!.WindowState = WindowState.Minimized;

    private void OnMaximize(object? sender, RoutedEventArgs e) => ToggleMaximize();

    /// <summary>
    /// Drag the window when pressing the bar (outside the buttons); double
    /// click toggles maximize/restore (native behavior of system title bars).
    /// </summary>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (window == null) return;

        var point = e.GetCurrentPoint(this);
        if (point.Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed) return;

        // Clicks on traffic-light buttons must not start a drag.
        if (e.Source is Visual source && source.FindAncestorOfType<Button>() != null) return;

        if (e.ClickCount >= 2)
        {
            ToggleMaximize();
            return;
        }

        window.BeginMoveDrag(e);
    }

    private void ToggleMaximize()
    {
        if (window == null) return;
        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    /// <summary>
    /// The green button icon follows the real window state (maximized via any
    /// path — button, double click, Win+Up, drag to top edge, taskbar shortcut).
    /// </summary>
    private void UpdateMaximizeState()
    {
        var maximized = window?.WindowState == WindowState.Maximized;
        MaximizeIcon.IsVisible = !maximized;
        RestoreIcon.IsVisible = maximized;
        ToolTip.SetTip(MaximizeButton, maximized ? Locale.TrafficLight_Restore : Locale.TrafficLight_Maximize);
    }
}
