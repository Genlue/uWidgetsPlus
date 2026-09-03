using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Services;
using uWidgets.Services;
using uWidgets.Views;

namespace uWidgets;

public class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection()
            .AddSingleton<IAppSettingsProvider, AppSettingsProvider>()
            .AddSingleton<ILayoutProvider, LayoutProvider>()
            .AddSingleton<IAssemblyProvider, AssemblyProvider>()
            .AddSingleton<WallpaperThemeService>()
            .AddSingleton<IThemeService, ThemeService>()
            .AddSingleton<ILocaleService, LocaleService>()
            .AddSingleton<IGridService<Widget>, GridService>()
            .AddSingleton<DisplayMonitorService>()
            .AddSingleton<IWidgetFactory<Window, UserControl>, WidgetFactory>()
            .AddSingleton<Settings, Settings>()
            .AddSingleton<UpdateService, UpdateService>()
            .BuildServiceProvider();

        var appSettingsProvider = services
            .GetRequiredService<IAppSettingsProvider>();

        var themeService = services
            .GetRequiredService<IThemeService>();

        var localeService = services
            .GetRequiredService<ILocaleService>();
        
        localeService.SetCulture(appSettingsProvider.Get().Region.Language);
        themeService.Apply(appSettingsProvider.Get().Theme);

        var displayMonitor = services.GetRequiredService<DisplayMonitorService>();
        var widgetFactory = (WidgetFactory) services.GetRequiredService<IWidgetFactory<Window, UserControl>>();

        // Multi-screen: a permanent invisible anchor window keeps an Avalonia
        // TopLevel alive so the monitor service can enumerate + watch screens even
        // when no widget/settings window exists yet. Order matters: the screen
        // list must be refreshed BEFORE widget creation so widgets are only
        // created for screens that are actually attached.
        var anchor = new WidgetAnchorWindow();
        anchor.ShowAnchored();
        displayMonitor.Attach(anchor);

        // Hot-plug: hide widgets of unplugged screens, recreate widgets when a
        // screen comes back (their per-screen configuration is still on disk).
        displayMonitor.ScreensChanged += (_, _) => widgetFactory.OnScreensChanged();

        var widgetsCount = widgetFactory
            .Create()
            .Select(widget =>
            {
                widget.Show();
                return widget;
            })
            .Count();
        
        if ((ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { Args.Length: > 0 } desktop && desktop.Args[0] == "--settings") || widgetsCount == 0)
            services.GetRequiredService<Settings>().Show();
        
        services.GetRequiredService<UpdateService>().CheckForUpdates();
        
        base.OnFrameworkInitializationCompleted();
    }
}