using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models.Settings;
using uWidgets.Services;
using uWidgets.ViewModels;

namespace uWidgets.Views;

public partial class Settings : Window
{
    private readonly SettingsViewModel viewModel;
    private readonly IAppSettingsProvider appSettingsProvider;

    public Settings(IAppSettingsProvider appSettingsProvider, IAssemblyProvider assemblyProvider, 
        ILayoutProvider layoutProvider, DisplayMonitorService displayMonitor, IWidgetFactory<Window, UserControl> widgetFactory,
        ProfileService profileService)
    {
        viewModel = new SettingsViewModel(appSettingsProvider, assemblyProvider, layoutProvider, displayMonitor, widgetFactory, profileService);
        this.appSettingsProvider = appSettingsProvider;
        DataContext = viewModel;
        Resized += OnResized;
        KeyDown += OnKeyDown;
        Unloaded += OnUnloaded;
        appSettingsProvider.DataChanged += (_, _, _) =>
        {
            ApplyTransparencyHint();
            ApplyTitleBarStyle();
        };
        InitializeComponent();
        ListBox.SelectedItem = viewModel.DefaultItem;
        ApplyTransparencyHint();
        ApplyTitleBarStyle();
    }

    /// <summary>
    /// Native transparency of the settings window, kept consistent with the
    /// active surface (AcrylicBlur for glass, Transparent for solid). Local
    /// value, so runtime surface switches reconfigures the existing window.
    /// </summary>
    private void ApplyTransparencyHint()
    {
        var theme = appSettingsProvider.Get().Theme;
        GlassSurface.Material = theme;
        GlassSurface.IsVisible = theme.IsLiquidGlass;
        TransparencyLevelHint = theme.UsesNativeBlur
            ? [WindowTransparencyLevel.AcrylicBlur]
            : [WindowTransparencyLevel.Transparent];
    }

    /// <summary>
    /// Native window chrome configuration for the selected title bar style.
    /// "Native" keeps the system window buttons; "TrafficLights" hides them
    /// (NoChrome) and lets the custom 36px macOS-style bar own the top row.
    /// These properties are updated live by the Win32 platform impl, so the
    /// switch takes effect immediately.
    /// </summary>
    private void ApplyTitleBarStyle()
    {
        var settings = appSettingsProvider.Get();
        var isTraffic = settings.EffectiveTitleBarStyle == TitleBarStyle.TrafficLights;
        ExtendClientAreaChromeHints = isTraffic
            ? ExtendClientAreaChromeHints.NoChrome
            : ExtendClientAreaChromeHints.Default;
        ExtendClientAreaTitleBarHeightHint = isTraffic ? 36 : 50;
        TrafficBar.IsVisible = isTraffic;
        TrafficBar.Size = settings.EffectiveTitleBarSize;
        AppTitle.IsVisible = !isTraffic;
    }
    
    private void Restart(object? sender, RoutedEventArgs e) => AppRestart.Restart();

    private void Exit(object? sender, RoutedEventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopApp) 
            desktopApp.Shutdown();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        AppTitle.Text = "UwUidgets";
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        Resized -= OnResized;
        KeyDown -= OnKeyDown;
        Unloaded -= OnUnloaded;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        System.Threading.Tasks.Task.Delay(500).ContinueWith(_ => InteropService.TrimProcessMemory());
    }

    private void OnResized(object? sender, WindowResizedEventArgs e) => 
        SplitView.IsPaneOpen = Width >= 800;

    private void OnMenuItemChanged(object? _, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is PageViewModel item)
        {
            if (item.IsItem)
            {
                viewModel.SetCurrentPage(item);
            }
            else
            {
                // Revert selection if user clicked a non-item (header or separator)
                if (viewModel.CurrentPageTitle != null)
                {
                    var prev = viewModel.AllItems.FirstOrDefault(x => x.Text == viewModel.CurrentPageTitle);
                    if (prev != null)
                    {
                        ListBox.SelectedItem = prev;
                        return;
                    }
                }
                ListBox.SelectedItem = viewModel.DefaultItem;
            }
        }
    }
}
