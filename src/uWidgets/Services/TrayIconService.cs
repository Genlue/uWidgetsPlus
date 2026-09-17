using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models.Settings;
using uWidgets.Locales;
// Alias: "Settings" alone is ambiguous with the uWidgets.Core.Models.Settings namespace.
using SettingsWindow = uWidgets.Views.Settings;

namespace uWidgets.Services;

/// <summary>
/// Notification-area (tray) icon and its right-click menu.
/// <para>
/// The app lives on the desktop with no main window, so the tray is the only always-reachable way
/// back to it (and the only way to quit with widgets on screen). The icon can be hidden from its
/// own menu; the widget context menu switches it back on, so hiding it is never a dead end.
/// </para>
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private const string IconUri = "avares://uWidgets/Assets/icon.ico";

    private readonly IAppSettingsProvider appSettingsProvider;
    private readonly Func<SettingsWindow> settingsWindow;
    private readonly WidgetFactory widgetFactory;

    private TrayIcon? trayIcon;
    private NativeMenuItem? widgetsItem;

    public TrayIconService(IAppSettingsProvider appSettingsProvider, Func<SettingsWindow> settingsWindow,
        WidgetFactory widgetFactory)
    {
        this.appSettingsProvider = appSettingsProvider;
        this.settingsWindow = settingsWindow;
        this.widgetFactory = widgetFactory;
    }

    /// <summary>The icon is on screen (the setting says so and it could actually be created).</summary>
    public bool IsIconVisible => trayIcon?.IsVisible == true;

    public void Start()
    {
        if (trayIcon != null) return;

        var application = Application.Current;
        if (application == null) return;

        var menu = BuildMenu();

        trayIcon = new TrayIcon
        {
            Icon = LoadIcon(),
            ToolTipText = "uWidgets+",
            Menu = menu,
            IsVisible = appSettingsProvider.Get().ShowTrayIcon
        };

        // A left click is the shortest way back into the app.
        trayIcon.Clicked += (_, _) => OpenSettings();

        TrayIcon.SetIcons(application, new TrayIcons { trayIcon });

        // The widget context menu can switch the icon back on; follow the setting.
        appSettingsProvider.DataChanged += OnSettingsChanged;
    }

    private NativeMenu BuildMenu()
    {
        widgetsItem = new NativeMenuItem(WidgetsMenuTitle());
        widgetsItem.Click += (_, _) => ToggleWidgets();

        var openItem = new NativeMenuItem(Locale.Tray_OpenSettings);
        openItem.Click += (_, _) => OpenSettings();

        var hideIconItem = new NativeMenuItem(Locale.Tray_HideIcon);
        hideIconItem.Click += (_, _) => SetIconVisible(false);

        var exitItem = new NativeMenuItem(Locale.Tray_Exit);
        exitItem.Click += (_, _) => AppShutdown.Request();

        var menu = new NativeMenu();
        menu.Add(openItem);
        menu.Add(widgetsItem);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(hideIconItem);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(exitItem);
        return menu;
    }

    private void OpenSettings()
    {
        var window = settingsWindow();
        window.ShowAndActivate();
    }

    private void ToggleWidgets()
    {
        widgetFactory.SetWidgetsHidden(!widgetFactory.WidgetsHidden);
        UpdateWidgetsItem();
    }

    /// <summary>Label of the widget toggle: it always describes what the next click does.</summary>
    private string WidgetsMenuTitle() =>
        widgetFactory.WidgetsHidden ? Locale.Tray_ShowWidgets : Locale.Tray_HideWidgets;

    private void UpdateWidgetsItem()
    {
        if (widgetsItem != null) widgetsItem.Header = WidgetsMenuTitle();
    }

    /// <summary>Show or hide the tray icon and remember the choice.</summary>
    public void SetIconVisible(bool visible)
    {
        var settings = appSettingsProvider.Get();
        if (settings.ShowTrayIcon == visible) return;

        appSettingsProvider.Save(settings with { ShowTrayIcon = visible });
    }

    private void OnSettingsChanged(object sender, AppSettings? oldData, AppSettings newData)
    {
        if (trayIcon == null) return;
        if (oldData?.ShowTrayIcon == newData.ShowTrayIcon) return;

        trayIcon.IsVisible = newData.ShowTrayIcon;
    }

    /// <summary>
    /// The tray icon of the shipped exe. A failure here must not take the app down — the icon
    /// then simply stays blank instead of the process failing to start.
    /// </summary>
    private static WindowIcon? LoadIcon()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri(IconUri));
            return new WindowIcon(stream);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        appSettingsProvider.DataChanged -= OnSettingsChanged;

        if (trayIcon != null)
        {
            if (Application.Current != null)
                TrayIcon.SetIcons(Application.Current, null);

            trayIcon.Dispose();
            trayIcon = null;
        }
    }
}
