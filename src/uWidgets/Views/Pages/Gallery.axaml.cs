using System;
using System.Collections.Generic;
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

public partial class Gallery : UserControl
{
    private readonly IAppSettingsProvider appSettingsProvider;
    private readonly ILayoutProvider layoutProvider;
    private readonly IAssemblyProvider assemblyProvider;
    private readonly AssemblyInfo assemblyInfo;
    private readonly IWidgetFactory<Window, UserControl> widgetFactory;
    private readonly DisplayMonitorService displayMonitor;
    public List<WidgetPreviewViewModel> Widgets => GetWidgets();
    public int WidgetSize => 160;
    public CornerRadius Radius => new(appSettingsProvider.Get().Dimensions.Radius / (VisualRoot?.RenderScaling ?? 1.0));

    public Gallery(IAppSettingsProvider appSettingsProvider, ILayoutProvider layoutProvider, IAssemblyProvider assemblyProvider, 
        AssemblyInfo assemblyInfo, IWidgetFactory<Window, UserControl> widgetFactory, DisplayMonitorService displayMonitor)
    {
        this.appSettingsProvider = appSettingsProvider;
        this.layoutProvider = layoutProvider;
        this.assemblyProvider = assemblyProvider;
        this.assemblyInfo = assemblyInfo;
        this.widgetFactory = widgetFactory;
        this.displayMonitor = displayMonitor;
        DataContext = this;
        Unloaded += OnUnloaded;
        
        InitializeComponent();
    }

    private List<WidgetPreviewViewModel> GetWidgets()
    {
        var assembly = assemblyProvider
            .LoadAssembly(assemblyInfo.AssemblyName);

        var locale = assemblyProvider.GetLocaleResourceManager(assembly);

        var widgets =  assembly
            .GetCustomAttributes<WidgetInfoAttribute>()
            .Select(widgetInfo => new WidgetPreviewViewModel(
                widgetFactory.CreateControl(widgetInfo.ViewType),
                assemblyInfo.AssemblyName,
                widgetInfo.ViewType.Name,
                locale?.GetString(widgetInfo.Title ?? string.Empty),
                locale?.GetString(widgetInfo.Subtitle ?? string.Empty),
                widgetInfo.DefaultColumns,
                widgetInfo.DefaultRows
            ))
            .ToList();

        return widgets;
    }
    
    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        // Multi-screen layout: unload the widget assembly only when no screen has
        // a widget of this type anymore (flattened across all per-screen layouts).
        if (layoutProvider.Get().AllWidgets.All(x => x.Type != assemblyInfo.AssemblyName))
        {
            assemblyProvider.UnloadAssembly(assemblyInfo.AssemblyName);
        }
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