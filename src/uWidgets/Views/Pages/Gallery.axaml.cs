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
    public List<WidgetPreviewViewModel> Widgets => GetWidgets();
    public int WidgetSize => 160;
    public CornerRadius Radius => new(appSettingsProvider.Get().Dimensions.Radius / (VisualRoot?.RenderScaling ?? 1.0));

    public Gallery(IAppSettingsProvider appSettingsProvider, ILayoutProvider layoutProvider, IAssemblyProvider assemblyProvider, 
        AssemblyInfo assemblyInfo, IWidgetFactory<Window, UserControl> widgetFactory)
    {
        this.appSettingsProvider = appSettingsProvider;
        this.layoutProvider = layoutProvider;
        this.assemblyProvider = assemblyProvider;
        this.assemblyInfo = assemblyInfo;
        this.widgetFactory = widgetFactory;
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
                locale?.GetString(widgetInfo.Subtitle ?? string.Empty)
            ))
            .ToList();

        return widgets;
    }
    
    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (layoutProvider.Get().All(x => x.Type != assemblyInfo.AssemblyName))
        {
            assemblyProvider.UnloadAssembly(assemblyInfo.AssemblyName);
        }
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var viewModel = button!.DataContext as WidgetPreviewViewModel;
        var settings = appSettingsProvider.Get();
        var position = button.PointToScreen(new Point(0, 0));

        int x = position.X, y = position.Y, size;
        if (settings.Layout.GridMode == GridMode.Manual)
        {
            // Manual grid: 1×1 cell by default, snapped to the nearest cell.
            // Grid metrics are physical; window size is a DIP → convert.
            var screen = (VisualRoot as Window)?.Screens.Primary;
            var area = screen?.WorkingArea;
            var (cell, gridX, gridY) = GridMetrics.Resolve(
                settings.Grid,
                area?.X ?? 0, area?.Y ?? 0, area?.Width ?? 1920, area?.Height ?? 1080);
            var scaling = screen?.Scaling ?? 1.0;
            size = (int) Math.Round(cell / scaling);
            x = gridX + (int) Math.Round((position.X - gridX) / (double) cell) * cell;
            y = gridY + (int) Math.Round((position.Y - gridY) / (double) cell) * cell;
        }
        else
        {
            var dimensions = settings.Dimensions;
            size = 2 * dimensions.Size + dimensions.Margin;
        }

        var widgetSettings = new WidgetLayout(viewModel!.Type, viewModel.Subtype, x, y, size, size, null);
        widgetFactory.Add(widgetSettings).Show();
    }
}