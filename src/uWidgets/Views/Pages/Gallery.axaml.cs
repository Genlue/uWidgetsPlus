using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;
using uWidgets.Core.Models.Attributes;
using uWidgets.Core.Models.Settings;
using uWidgets.Services;
using uWidgets.ViewModels;

namespace uWidgets.Views.Pages;

public partial class Gallery : UserControl, INotifyPropertyChanged
{
    private readonly IAppSettingsProvider appSettingsProvider;
    private readonly ILayoutProvider layoutProvider;
    private readonly IAssemblyProvider assemblyProvider;
    private readonly List<AssemblyInfo> assemblyInfos;
    private readonly IWidgetFactory<Window, UserControl> widgetFactory;
    private readonly DisplayMonitorService displayMonitor;
    private List<WidgetPreviewViewModel>? widgets;
    public List<WidgetPreviewViewModel> Widgets => widgets ??= GetWidgets();
    public CornerRadius Radius => new(appSettingsProvider.Get().Dimensions.Radius / (VisualRoot?.RenderScaling ?? 1.0));

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    public Gallery(IAppSettingsProvider appSettingsProvider, ILayoutProvider layoutProvider, IAssemblyProvider assemblyProvider, 
        AssemblyInfo assemblyInfo, IWidgetFactory<Window, UserControl> widgetFactory, DisplayMonitorService displayMonitor)
        : this(appSettingsProvider, layoutProvider, assemblyProvider, [assemblyInfo], widgetFactory, displayMonitor)
    {
    }

    public Gallery(IAppSettingsProvider appSettingsProvider, ILayoutProvider layoutProvider, IAssemblyProvider assemblyProvider, 
        IEnumerable<AssemblyInfo> assemblyInfos, IWidgetFactory<Window, UserControl> widgetFactory, DisplayMonitorService displayMonitor)
    {
        this.appSettingsProvider = appSettingsProvider;
        this.layoutProvider = layoutProvider;
        this.assemblyProvider = assemblyProvider;
        this.assemblyInfos = assemblyInfos.ToList();
        this.widgetFactory = widgetFactory;
        this.displayMonitor = displayMonitor;
        DataContext = this;
        
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// Every card hosts a real, live widget control — that is what makes the preview look exactly
    /// like the desktop widget — so the card list owns timers, WMI/SMTC subscriptions and decoded
    /// bitmaps for all ~28 widget types at once.
    /// <para>
    /// The settings window caches its pages, so a gallery that is navigated away from comes back
    /// later: the previews are released when the page leaves the tree (each control cleans itself
    /// up when it unloads) and rebuilt on the next visit. Keeping the disposed controls in the
    /// card list would show dead previews, and keeping the live ones alive would leak a whole
    /// widget set per visit.
    /// </para>
    /// </summary>
    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (widgets != null) return;
        widgets = GetWidgets();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Widgets)));
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e) => ReleasePreviews();

    private void ReleasePreviews()
    {
        if (widgets == null) return;

        var released = widgets;
        widgets = null;
        foreach (var preview in released)
        {
            try
            {
                (preview.Control as IDisposable)?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Gallery] Failed to release {preview.Type}/{preview.Subtype}: {ex.Message}");
            }
        }
    }

    private List<WidgetPreviewViewModel> GetWidgets()
    {
        var result = new List<WidgetPreviewViewModel>();

        foreach (var info in assemblyInfos)
        {
            try
            {
                var assembly = assemblyProvider.LoadAssembly(info.AssemblyName);
                var locale = assemblyProvider.GetLocaleResourceManager(assembly);

                var items = assembly
                    .GetCustomAttributes<WidgetInfoAttribute>()
                    .Select(widgetInfo => new WidgetPreviewViewModel(
                        widgetFactory.CreateControl(widgetInfo.ViewType),
                        info.AssemblyName,
                        widgetInfo.ViewType.Name,
                        locale?.GetString(widgetInfo.Title ?? string.Empty) ?? widgetInfo.Title,
                        locale?.GetString(widgetInfo.Subtitle ?? string.Empty) ?? widgetInfo.Subtitle,
                        widgetInfo.DefaultColumns,
                        widgetInfo.DefaultRows
                    ));

                result.AddRange(items);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Gallery] Failed to load {info.AssemblyName}: {ex.Message}");
            }
        }

        return result;
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var preview = button!.DataContext as WidgetPreviewViewModel;
        if (preview == null) return;

        // Absolute screen position of the clicked cell (physical pixels).
        var pointer = button.PointToScreen(new Point(0, 0));
        var settingsWindow = VisualRoot as Window;
        var attached = settingsWindow != null ? displayMonitor.Find(settingsWindow) : null;
        if (attached == null)
        {
            // Monitor not ready (edge case): legacy primary placement.
            var legacy = layoutProvider.Get().FindById(ScreensLayout.LegacyPrimaryId)
                         ?? new ScreenLayout(ScreensLayout.LegacyPrimaryId, null, null, null, null, null, []);
            var (defaultW, defaultH) = DefaultSize(settingsWindow, preview.DefaultColumns, preview.DefaultRows);
            var legacyLayout = new WidgetLayout(preview.Type, preview.Subtype, pointer.X, pointer.Y, 
                defaultW, defaultH, null);
            widgetFactory.Add(legacy, legacyLayout).Show();
            return;
        }

        var screenConfig = attached.Config ?? displayMonitor.EnsureConfig(attached);
        var (x, y, w, h) = ComputePlacement(screenConfig, attached, new Point(pointer.X, pointer.Y), preview.DefaultColumns, preview.DefaultRows);
        var widgetLayout = new WidgetLayout(preview.Type, preview.Subtype, x, y, w, h, null);
        widgetFactory.Add(screenConfig, widgetLayout).Show();
    }

    /// <summary>
    /// Compute the initial placement (position relative to the owning screen's
    /// working area + size) for a new widget on the target screen.
    /// </summary>
    private (int X, int Y, int Width, int Height) ComputePlacement(
        ScreenLayout screenConfig, AttachedScreen attached, Point pointer, int defaultCols = 2, int defaultRows = 2)
    {
        var settings = appSettingsProvider.Get();
        var screen = attached.Screen;
        var area = screen.WorkingArea;

        int width;
        int height;

        if (settings.Layout.GridMode != GridMode.Manual)
        {
            width = (int) (defaultCols * settings.Dimensions.Size + (defaultCols - 1) * settings.Dimensions.Margin);
            height = (int) (defaultRows * settings.Dimensions.Size + (defaultRows - 1) * settings.Dimensions.Margin);
            return ((int)(pointer.X - area.X), (int)(pointer.Y - area.Y), width, height);
        }

        var grid = screenConfig.Grid ?? settings.Grid ?? uWidgets.Core.Models.Settings.Grid.Default;
        var (cell, gridX, gridY) = GridMetrics.Resolve(grid, area.X, area.Y, area.Width, area.Height);
        var scaling = screen.Scaling;
        width = (int) Math.Round(defaultCols * cell / scaling);
        height = (int) Math.Round(defaultRows * cell / scaling);
        var x = gridX + (int) Math.Round((pointer.X - gridX) / (double) cell) * cell;
        var y = gridY + (int) Math.Round((pointer.Y - gridY) / (double) cell) * cell;
        return (x - area.X, y - area.Y, width, height);
    }

    private (int Width, int Height) DefaultSize(Window? settingsWindow, int defaultCols = 2, int defaultRows = 2)
    {
        var settings = appSettingsProvider.Get();
        if (settings.Layout.GridMode == GridMode.Manual)
        {
            var screen = settingsWindow?.Screens.Primary;
            var area = screen?.WorkingArea;
            var (cell, _, _) = GridMetrics.Resolve(settings.Grid, area?.X ?? 0, area?.Y ?? 0, area?.Width ?? 1920, area?.Height ?? 1080);
            var scaling = screen?.Scaling ?? 1.0;
            return ((int) Math.Round(defaultCols * cell / scaling), (int) Math.Round(defaultRows * cell / scaling));
        }
        var w = (int) (defaultCols * settings.Dimensions.Size + (defaultCols - 1) * settings.Dimensions.Margin);
        var h = (int) (defaultRows * settings.Dimensions.Size + (defaultRows - 1) * settings.Dimensions.Margin);
        return (w, h);
    }
}