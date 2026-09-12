using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ReactiveUI;
using uWidgets.Core;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;
using uWidgets.Locales;
using uWidgets.Services;
using uWidgets.Views.Pages;

namespace uWidgets.ViewModels;

public class SettingsViewModel : ReactiveObject
{
    private readonly IAppSettingsProvider appSettingsProvider;
    private readonly IAssemblyProvider assemblyProvider;
    private readonly ILayoutProvider layoutProvider;
    private readonly DisplayMonitorService displayMonitor;
    private readonly IWidgetFactory<Window, UserControl> widgetFactory;

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

    public PageViewModel[] AllItems { get; }

    public PageViewModel DefaultItem => AllItems.FirstOrDefault(x => x.Type == typeof(Appearance)) ?? AllItems.First(x => x.IsItem);

    public SettingsViewModel(
        IAppSettingsProvider appSettingsProvider,
        IAssemblyProvider assemblyProvider,
        ILayoutProvider layoutProvider,
        DisplayMonitorService displayMonitor,
        IWidgetFactory<Window, UserControl> widgetFactory)
    {
        this.appSettingsProvider = appSettingsProvider;
        this.assemblyProvider = assemblyProvider;
        this.layoutProvider = layoutProvider;
        this.displayMonitor = displayMonitor;
        this.widgetFactory = widgetFactory;

        // 1. Load and deduplicate widget assemblies
        var loadedAssemblies = assemblyProvider
            .GetAssemblyInfos(Const.WidgetsFolder)
            .ToDictionary(
                group => group.Key,
                group => group.MaxBy(assembly => assembly.Version)!);

        List<AssemblyInfo> GetAssemblies(params string[] names) =>
            names.Where(n => loadedAssemblies.ContainsKey(n))
                 .Select(n => loadedAssemblies[n])
                 .ToList();

        var allAssemblies = loadedAssemblies.Values.ToList();
        var fixedAssemblies = GetAssemblies("FixedWidgets");
        var timeAssemblies = GetAssemblies("Clock", "Calendar", "Progress");
        var prodAssemblies = GetAssemblies("Notes", "Reminders", "Folders");
        var toolsAssemblies = GetAssemblies("Tools", "Search");
        var monitorAssemblies = GetAssemblies("Monitor");
        var mediaAssemblies = GetAssemblies("Music", "Weather", "Picture");

        var knownNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Clock", "Calendar", "Notes", "Reminders", "Folders", "Tools", "Search", "Monitor", "Music", "Weather", "Progress", "Picture", "FixedWidgets"
        };
        var otherAssemblies = loadedAssemblies.Values
            .Where(a => !knownNames.Contains(a.AssemblyName))
            .ToList();

        // 2. Build structured category navigation
        var items = new List<PageViewModel>
        {
            // --- Section 1: 系统设置 ---
            new(null, null, "系统设置"),
            new(typeof(General), GetIcon(nameof(General)), Locale.Settings_General),
            new(typeof(Appearance), GetIcon(nameof(Appearance)), Locale.Settings_Appearance),
            new(typeof(Advanced), GetIcon(nameof(Advanced)), Locale.Settings_Advanced),
            new(typeof(MultiScreen), GetIcon(nameof(MultiScreen)), Locale.Settings_MultiScreen),
            new(typeof(About), GetIcon(nameof(About)), Locale.Settings_About),

            // Separator between sections
            new(null, null, string.Empty),

            // --- Section 2: 小组件库 ---
            new(null, null, "小组件库"),
            new(typeof(Gallery), SafeParseIcon("M4 4h7v7H4V4zm9 0h7v7h-7V4zm-9 9h7v7H4v-7zm9 0h7v7h-7v-7z"), "全部组件", null, allAssemblies),
            new(typeof(Gallery), SafeParseIcon("M3 3h8v8H3V3zm10 0h8v8h-8V3zM3 13h8v8H3v-8zm10 0h8v8h-8v-8z"), "固定组件", null, fixedAssemblies),
            new(typeof(Gallery), SafeParseIcon("M12 2C6.5 2 2 6.5 2 12s4.5 10 10 10 10-4.5 10-10S17.5 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8zm.5-13H11v6l5.2 3.2.8-1.3-4.5-2.7V7z"), "时间日程", null, timeAssemblies),
            new(typeof(Gallery), SafeParseIcon("M19 3h-4.18C14.4 1.84 13.3 1 12 1c-1.3 0-2.4.84-2.82 2H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-7 0c.55 0 1 .45 1 1s-.45 1-1 1-1-.45-1-1 .45-1 1-1zm2 14H7v-2h7v2zm3-4H7v-2h10v2zm0-4H7V7h10v2z"), "日常效率", null, prodAssemblies),
            new(typeof(Gallery), SafeParseIcon("M22.7 19l-9.1-9.1c.9-2.3.4-5-1.5-6.9-2-2-5-2.4-7.4-1.3L9 6 6 9 1.7 4.7C.6 7.1 1 10.1 3 12.1c1.9 1.9 4.6 2.4 6.9 1.5l9.1 9.1c.4.4 1 .4 1.4 0l2.3-2.3c.4-.4.4-1 0-1.4z"), "实用工具", null, toolsAssemblies),
            new(typeof(Gallery), SafeParseIcon("M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-7 14l-2.5-4.5-1.5 2.5H5v-2h2.5l1.5-2.5 2.5 4.5 3-6 2 4H19v2h-3.5l-2-4-1.5 3.5z"), "系统性能", null, monitorAssemblies),
            new(typeof(Gallery), SafeParseIcon("M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z"), "影音相册", null, mediaAssemblies),
        };

        if (otherAssemblies.Count > 0)
        {
            items.Add(new(typeof(Gallery), SafeParseIcon("M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z"), "其他扩展", null, otherAssemblies));
        }

        AllItems = items.ToArray();
    }

    private static StreamGeometry? SafeParseIcon(string? data)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;
        try { return StreamGeometry.Parse(data); }
        catch { return null; }
    }

    private static StreamGeometry? GetIcon(string name) =>
        (StreamGeometry?)(Application.Current!.TryFindResource(name, out var icon) ? icon : null);

    private readonly Dictionary<PageViewModel, UserControl> pageCache = new();

    public void SetCurrentPage(PageViewModel? value)
    {
        if (value == null || !value.IsItem) return;

        if (!pageCache.TryGetValue(value, out var page))
        {
            if (value.Type == typeof(Gallery))
            {
                var assemblies = value.AssemblyInfos 
                    ?? (value.AssemblyInfo != null ? (IEnumerable<AssemblyInfo>)[value.AssemblyInfo] : Array.Empty<AssemblyInfo>());
                page = new Gallery(appSettingsProvider, layoutProvider, assemblyProvider, assemblies, widgetFactory, displayMonitor);
            }
            else
            {
                page = value.Type switch
                {
                    var type when type == typeof(Advanced) => new Advanced(appSettingsProvider, layoutProvider, displayMonitor),
                    var type when type == typeof(MultiScreen) => new MultiScreen(appSettingsProvider, layoutProvider, displayMonitor),
                    _ => (UserControl?) Activator.CreateInstance(value.Type, appSettingsProvider)
                };
            }

            if (page != null)
            {
                pageCache[value] = page;
            }
        }

        CurrentPage = page;
        CurrentPageTitle = value.Text;
    }
}