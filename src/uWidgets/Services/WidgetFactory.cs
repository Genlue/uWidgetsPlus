using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;
using uWidgets.Core.Models.Attributes;
using uWidgets.Core.Services;
using uWidgets.Views;

namespace uWidgets.Services;

public class WidgetFactory(IAssemblyProvider assemblyProvider, ILayoutProvider layoutProvider, DisplayMonitorService displayMonitor)
    : IWidgetFactory<Window, UserControl>
{
    private readonly Dictionary<string, List<Widget>> activeWidgets = [];

    /// <summary>True while <see cref="SuspendAll"/> is in effect (widgets hidden, timers paused).</summary>
    private bool suspended;

    /// <summary>
    /// Create widget windows for every attached screen that has a stored configuration
    /// (widgets of screens that are currently unplugged stay in their config, hidden).
    /// </summary>
    public IEnumerable<Window> Create()
    {
        // Split legacy primary widgets onto their real screens before creation
        // (idempotent; the imported/migrated v2 file then renders widgets on the
        // correct per-screen entries with correct per-screen grids).
        displayMonitor.MigrateLegacyWidgets();

        return displayMonitor.Attached
            .Where(screen => screen.Config != null)
            .SelectMany(screen => activeWidgets.ContainsKey(screen.Config!.Id)
                ? [] // already created (e.g. by a hot-plug pass during startup) — never twice
                : screen.Config!.Layout.Select(layout => CreateInternal(screen.Config!, layout)))
            .ToList();
    }

    /// <summary>
    /// Add a widget to a screen configuration and create its window.
    /// </summary>
    /// <param name="screen">The target screen configuration (may be a new entry — it is upserted).</param>
    /// <param name="widgetLayout">The widget layout to add.</param>
    public Window Add(ScreenLayout screen, WidgetLayout widgetLayout)
    {
        var withWidget = screen with { Layout = [.. screen.Layout, widgetLayout] };
        layoutProvider.Save(layoutProvider.Get().UpsertScreen(withWidget));
        return CreateInternal(withWidget, widgetLayout);
    }

    /// <summary>
    /// Legacy single-argument add (interface member): places the widget on the
    /// "primary" entry (v1 semantics — absolute desktop coordinates). Kept as the
    /// interface contract for pre-multi-screen callers.
    /// </summary>
    public Window Add(WidgetLayout widgetLayout)
    {
        var screens = layoutProvider.Get();
        var primary = screens.FindById(ScreensLayout.LegacyPrimaryId)
                      ?? screens.Screens.FirstOrDefault()
                      ?? new ScreenLayout(ScreensLayout.LegacyPrimaryId, null, null, null, null, null, []);
        return Add(primary, widgetLayout);
    }

    public UserControl CreateControl(Type type)
    {
        var previewScreenId = layoutProvider.Get().Screens.FirstOrDefault()?.Id ?? ScreensLayout.LegacyPrimaryId;
        var widgetLayoutProvider = new WidgetLayoutProvider(layoutProvider, previewScreenId, null);
        return CreateWidgetControl(type, widgetLayoutProvider, null);
    }

    private Widget CreateInternal(ScreenLayout screen, WidgetLayout widgetLayout)
    {
        var widgetLayoutProvider = new WidgetLayoutProvider(layoutProvider, screen.Id, widgetLayout);

        var assembly = assemblyProvider.LoadAssembly(widgetLayout.Type);
        var widgetInfo = GetWidgetInfo(assembly, widgetLayout.SubType);
        var widgetControl = () => CreateWidgetControl(widgetInfo.ViewType, widgetLayoutProvider, widgetLayoutProvider.Get().GetModel(widgetInfo.ModelType));
        var settingsWindow = () => (Settings) assemblyProvider.Activate(typeof(Settings));

        var editWidgetWindow = widgetInfo.EditModelViewType != null
            ? () => CreateEditWidgetWindow(widgetLayoutProvider, widgetInfo.EditModelViewType)
            : (Func<EditWidget>?) null;

        var widget = editWidgetWindow != null
            ? (Widget) assemblyProvider.Activate(typeof(Widget), widgetLayoutProvider, widgetControl, settingsWindow, editWidgetWindow)
            : (Widget) assemblyProvider.Activate(typeof(Widget), widgetLayoutProvider, widgetControl, settingsWindow);

        if (!activeWidgets.TryGetValue(screen.Id, out var list))
            activeWidgets[screen.Id] = list = [];
        list.Add(widget);
        widget.Closed += (_, _) => list.Remove(widget);
        return widget;
    }

    /// <summary>
    /// Close every widget of a screen configuration (used when the screen is unplugged;
    /// the layout stays on disk, so replugging recreates them in place).
    /// </summary>
    public void CloseScreen(string screenId)
    {
        if (!activeWidgets.TryGetValue(screenId, out var list)) return;
        foreach (var widget in list.ToList())
            widget.Close();
        activeWidgets.Remove(screenId);
    }

    /// <summary>
    /// Close every active widget across all screens and recreate them from the current layout.
    /// </summary>
    public void RecreateAll()
    {
        CloseAll();
        CreateFromLayout();
    }

    /// <summary>
    /// Close every active widget window and forget it, without creating replacements.
    /// <para>
    /// Windows are hidden <b>before</b> being closed: a close request is only fully
    /// processed once the dispatcher gets to it, so the outgoing set would otherwise
    /// still be on screen while the incoming set is shown (the "two sets of widgets"
    /// flash when switching profiles). Each window also stops listening to layout
    /// changes first, so a window that is mid-teardown can never write its entry back
    /// into the layout that is being replaced.
    /// </para>
    /// </summary>
    public void CloseAll()
    {
        foreach (var list in activeWidgets.Values)
        {
            foreach (var widget in list.ToList())
            {
                try
                {
                    widget.PrepareForTeardown();
                    widget.Hide();
                    widget.Close();
                }
                catch
                {
                    // A window that already closed itself is not an error here.
                }
            }
        }

        activeWidgets.Clear();
    }

    /// <summary>
    /// Create and show the widget windows for every screen in the current layout.
    /// </summary>
    public void CreateFromLayout()
    {
        displayMonitor.Refresh();

        foreach (var win in Create())
        {
            win.Show();
        }
    }

    /// <summary>
    /// Hide every widget, release the caches the widget views opt into releasing and
    /// pause the shared timers — the desktop is covered by a fullscreen application,
    /// so nothing here would be visible and every megabyte counts.
    /// </summary>
    public void SuspendAll()
    {
        if (suspended) return;
        suspended = true;

        // Shared widget timers (clock ticks, monitor sampling, weather refresh …) are
        // driven by one DispatcherTimer per interval, so pausing them pauses every
        // widget at once.
        TimerService.PauseAll();

        foreach (var list in activeWidgets.Values)
        {
            foreach (var widget in list.ToList())
            {
                try
                {
                    widget.SuspendContent();
                    widget.Hide();
                }
                catch
                {
                    // Never let one misbehaving widget abort the whole suspend pass.
                }
            }
        }

        InteropService.TrimProcessMemory();
    }

    /// <summary>Show the widgets again, resume the timers and rebuild the released caches.</summary>
    public void ResumeAll()
    {
        if (!suspended) return;
        suspended = false;

        foreach (var list in activeWidgets.Values)
        {
            foreach (var widget in list.ToList())
            {
                try
                {
                    widget.Show();
                    widget.ResumeContent();
                }
                catch
                {
                    // See SuspendAll.
                }
            }
        }

        TimerService.ResumeAll();
    }

    /// <summary>
    /// React to display changes: hide widgets of unplugged screens, recreate widgets
    /// of screens that just came back (their config is still on disk).
    /// </summary>
    public void OnScreensChanged()
    {
        // A screen just arrived: legacy primary widgets whose absolute position
        // now falls on it are moved into its per-screen entry first (idempotent).
        displayMonitor.MigrateLegacyWidgets();

        var attached = displayMonitor.Attached;

        foreach (var screenId in activeWidgets.Keys.ToList())
            if (attached.All(screen => screen.Config?.Id != screenId))
                CloseScreen(screenId);

        foreach (var screen in attached)
        {
            if (screen.Config == null || activeWidgets.ContainsKey(screen.Config.Id)) continue;

            foreach (var layout in screen.Config.Layout)
                CreateInternal(screen.Config, layout).Show();
        }
    }

    private UserControl CreateWidgetControl(Type type, WidgetLayoutProvider? widgetLayoutProvider, object? model)
    {
        List<object> args = [];

        if (NeedsWidgetLayoutProvider(type) && widgetLayoutProvider != null)
            args.Add(widgetLayoutProvider);

        if (model != null)
            args.Add(model);

        return (assemblyProvider.Activate(type, args.ToArray()) as UserControl)!;
    }

    private EditWidget CreateEditWidgetWindow(IWidgetLayoutProvider widgetLayoutProvider, Type type)
    {
        var control = (UserControl) assemblyProvider.Activate(type, widgetLayoutProvider);

        return new EditWidget(widgetLayoutProvider, control);
    }

    private bool NeedsWidgetLayoutProvider(Type type)
    {
        return type
            .GetConstructors()
            .Any(constructor => constructor
                .GetParameters()
                .Any(param => param.ParameterType == typeof(IWidgetLayoutProvider)));
    }

    private static WidgetInfoAttribute GetWidgetInfo(Assembly assembly, string typeName)
    {
        var widgetInfo = assembly
            .GetCustomAttributes<WidgetInfoAttribute>()
            .SingleOrDefault(attribute => attribute.ViewType.Name == typeName);

        if (widgetInfo == null)
            throw new ArgumentException($"No suitable WidgetInfoAttribute found for {typeName}");

        return widgetInfo;
    }
}