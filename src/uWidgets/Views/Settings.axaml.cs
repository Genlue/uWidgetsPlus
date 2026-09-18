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
    private readonly WidgetFactory? widgetFactory;

    public Settings(IAppSettingsProvider appSettingsProvider, IAssemblyProvider assemblyProvider, 
        ILayoutProvider layoutProvider, DisplayMonitorService displayMonitor, IWidgetFactory<Window, UserControl> widgetFactory,
        ProfileService profileService)
    {
        viewModel = new SettingsViewModel(appSettingsProvider, assemblyProvider, layoutProvider, displayMonitor, widgetFactory, profileService);
        this.appSettingsProvider = appSettingsProvider;
        // Concrete type: HasWidgets is factory bookkeeping the shared SDK interface does not expose.
        this.widgetFactory = widgetFactory as WidgetFactory;
        DataContext = viewModel;
        Resized += OnResized;
        KeyDown += OnKeyDown;
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
    /// Show the shared settings window and bring it to the front.
    /// <para>
    /// All widgets open this one instance, and a hand-over from a second launch lands here, so
    /// this is the single entry point for surfacing the app.
    /// </para>
    /// </summary>
    public void ShowAndActivate()
    {
        if (!IsVisible)
        {
            // A minimized Win32 window sits at the classic (-32000, -32000) rect, so it has to be
            // brought back to Normal *before* it is shown or it is "shown" off-screen.
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;

            Show();

            // WindowStartupLocation="CenterScreen" re-applies on every Show, and it derives the
            // position from the window it is re-showing — for a hidden/minimized window that is a
            // stale rect, so the window jumped (onto another monitor, or off-screen entirely) each
            // time it was opened from the tray. Let it place the window exactly once, then hand
            // placement over to the position the user left it at.
            WindowStartupLocation = WindowStartupLocation.Manual;
        }

        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Activate();

        // Avalonia's Activate is not enough to raise a background window on Windows; the Win32
        // call is what actually pulls it in front of the user's current window.
        InteropService.BringToFront(this);
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
        GlassSurface.IsVisible = false;
        TransparencyLevelHint = (theme.UsesNativeBlur || theme.IsColorful || theme.IsLiquidGlass)
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

    private void Exit(object? sender, RoutedEventArgs e) => AppShutdown.Request();

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        AppTitle.Text = "UwUidgets";
    }

    /// <summary>
    /// Closing the shared settings window normally only hides it.
    /// <para>
    /// Avalonia cannot re-show a closed window, and every widget (plus a hand-over from a second
    /// launch) resolves this same instance, so closing it for real would leave the app with no
    /// settings window at all. A genuine exit announces itself through
    /// <see cref="AppShutdown.Request"/> and closes for real.
    /// </para>
    /// <para>
    /// With no widget window left there is nothing to reuse it for — and closing is then the only
    /// way out of the app — so that case closes for real too.
    /// </para>
    /// </summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!AppShutdown.IsShuttingDown && widgetFactory?.HasWidgets == true)
        {
            e.Cancel = true;
            Hide();
            InteropService.TrimProcessMemory();
            return;
        }

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
