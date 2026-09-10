using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ReactiveUI;
using uWidgets.Core;
using uWidgets.Core.Interfaces;
using uWidgets.Locales;
using uWidgets.Services;
using uWidgets.Views.Pages;

namespace uWidgets.ViewModels;

public class SettingsViewModel(IAppSettingsProvider appSettingsProvider, IAssemblyProvider assemblyProvider, 
    ILayoutProvider layoutProvider, DisplayMonitorService displayMonitor, IWidgetFactory<Window, UserControl> widgetFactory) : ReactiveObject
{
    private UserControl? currentPage;
    public UserControl? CurrentPage
    {
        get => currentPage;
        set => this.RaiseAndSetIfChanged(ref currentPage, value);
    }

    private string? currentPageTitle;
    public string? CurrentPageTitle
    {
        get => currentPageTitle;
        set => this.RaiseAndSetIfChanged(ref currentPageTitle, value);
    }

    public PageViewModel[] AllItems => MenuItems.Concat(widgetItems).ToArray();

    public static readonly PageViewModel[] MenuItems =
    [
        new PageViewModel(typeof(General), GetIcon(nameof(General)), Locale.Settings_General),
        new PageViewModel(typeof(Appearance), GetIcon(nameof(Appearance)), Locale.Settings_Appearance),
        new PageViewModel(typeof(Advanced), GetIcon(nameof(Advanced)), Locale.Settings_Advanced),
        new PageViewModel(typeof(MultiScreen), GetIcon(nameof(MultiScreen)), Locale.Settings_MultiScreen),
        new PageViewModel(typeof(About), GetIcon(nameof(About)),  Locale.Settings_About),
        new PageViewModel(null, null, null)
    ];

    private readonly PageViewModel[] widgetItems =
     assemblyProvider
            .GetAssemblyInfos(Const.WidgetsFolder)
            .ToDictionary(
                group => group.Key, 
                group => group.MaxBy(assembly => assembly.Version)!)
            .Select(assembly => new PageViewModel(
                    typeof(Gallery), SafeParseIcon(assembly.Value.IconData), assembly.Value.DisplayName, assembly.Value 
                    ))
            .OrderBy(page => page.Text)
            .ToArray();

    private static StreamGeometry? SafeParseIcon(string? data)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;
        try { return StreamGeometry.Parse(data); }
        catch { return null; }
    }

    private static StreamGeometry? GetIcon(string name) =>
        (StreamGeometry?)(Application.Current!.TryFindResource(name, out var icon) ? icon : null);

    public void SetCurrentPage(PageViewModel? value)
    {
        if (value?.Type == null) return;
        CurrentPage = value.AssemblyInfo == null
            ? value.Type switch
            {
                var type when type == typeof(Advanced) => new Advanced(appSettingsProvider, layoutProvider, displayMonitor),
                var type when type == typeof(MultiScreen) => new MultiScreen(appSettingsProvider, layoutProvider, displayMonitor),
                _ => (UserControl?) Activator.CreateInstance(value.Type, appSettingsProvider)
            }
            : new Gallery(appSettingsProvider, layoutProvider, assemblyProvider, value.AssemblyInfo, widgetFactory, displayMonitor);
        CurrentPageTitle = value?.Text;
    }
}