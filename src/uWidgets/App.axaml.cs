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
            .AddSingleton<IThemeService, ThemeService>()
            .AddSingleton<ILocaleService, LocaleService>()
            .AddSingleton<IGridService<Widget>, GridService>()
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

        var widgetsCount = services
            .GetRequiredService<IWidgetFactory<Window, UserControl>>()
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