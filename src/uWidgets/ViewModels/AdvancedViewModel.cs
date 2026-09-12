using System;
using System.Linq;
using ReactiveUI;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;
using uWidgets.Core.Models.Settings;
using uWidgets.Locales;
using uWidgets.Services;
using GridModeEnum = uWidgets.Core.Models.Settings.GridMode;

namespace uWidgets.ViewModels;

/// <summary>
/// A grid-mode option shown in the grid mode picker.
/// </summary>
public record GridModeOption(string Label, GridMode Value);

/// <summary>
/// An HTTP proxy option shown in the proxy picker.
/// </summary>
public record ProxyOption(string Label, string Value);

/// <summary>
/// Advanced page view model. The manual-grid numeric fields target the EFFECTIVE
/// grid of the primary screen — the same store and the same fallback chain the
/// full-screen grid editor uses: per-screen primary entry Grid →
/// <see cref="AppSettings.Grid"/> → <see cref="Grid.Default"/>.
/// <para>
/// This keeps the two editing surfaces (numeric boxes here, the visual editor)
/// on ONE configuration, so changing the grid in the editor is immediately
/// reflected by these fields (and vice versa). The view model re-raises its
/// grid properties whenever either provider publishes changes.
/// </para>
/// </summary>
public class AdvancedViewModel : ReactiveObject, IDisposable
{
    private readonly IAppSettingsProvider appSettingsProvider;
    private readonly ILayoutProvider layoutProvider;
    private readonly DisplayMonitorService displayMonitor;

    private readonly ProfileService? profileService;

    public AdvancedViewModel(IAppSettingsProvider appSettingsProvider, ILayoutProvider layoutProvider, DisplayMonitorService displayMonitor, ProfileService? profileService = null)
    {
        this.appSettingsProvider = appSettingsProvider;
        this.layoutProvider = layoutProvider;
        this.displayMonitor = displayMonitor;
        this.profileService = profileService;

        layoutProvider.DataChanged += OnLayoutDataChanged;
        appSettingsProvider.DataChanged += OnAppSettingsDataChanged;
        if (profileService != null)
        {
            profileService.ActiveProfileChanged += OnActiveProfileChanged;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        layoutProvider.DataChanged -= OnLayoutDataChanged;
        appSettingsProvider.DataChanged -= OnAppSettingsDataChanged;
        if (profileService != null)
        {
            profileService.ActiveProfileChanged -= OnActiveProfileChanged;
        }
    }

    private void OnActiveProfileChanged(object? sender, EventArgs e) => RaiseAllProperties();
    private void OnLayoutDataChanged(object? sender, ScreensLayout? oldData, ScreensLayout newData) => RaiseAllProperties();
    private void OnAppSettingsDataChanged(object? sender, AppSettings? oldData, AppSettings newData) => RaiseAllProperties();

    private void RaiseAllProperties()
    {
        this.RaisePropertyChanged(nameof(GridMode));
        this.RaisePropertyChanged(nameof(IsManualGrid));
        this.RaisePropertyChanged(nameof(GridColumns));
        this.RaisePropertyChanged(nameof(GridRows));
        this.RaisePropertyChanged(nameof(GridCellPercent));
        this.RaisePropertyChanged(nameof(GridXPercent));
        this.RaisePropertyChanged(nameof(GridYPercent));
        this.RaisePropertyChanged(nameof(SnapSize));
        this.RaisePropertyChanged(nameof(LockSize));
        this.RaisePropertyChanged(nameof(SnapPosition));
        this.RaisePropertyChanged(nameof(LockPosition));
        this.RaisePropertyChanged(nameof(Margin));
        this.RaisePropertyChanged(nameof(Radius));
        this.RaisePropertyChanged(nameof(ProxyMode));
        this.RaisePropertyChanged(nameof(ProxyCustomUrl));
        this.RaisePropertyChanged(nameof(UpdateUrl));
    }

    /// <summary>
    /// The grid that actually applies to the primary screen right now
    /// (per-screen primary entry → global <see cref="AppSettings.Grid"/> → default).
    /// </summary>
    private Grid EffectiveGrid =>
        displayMonitor.Attached.FirstOrDefault(screen => screen.Screen.Primary)?.Config?.Grid
        ?? appSettingsProvider.Get().Grid
        ?? Grid.Default;

    /// <summary>
    /// Persist a grid change to the SAME store the full-screen grid editor uses:
    /// the primary screen's per-screen entry when one exists, and the
    /// global <see cref="AppSettings.Grid"/>.
    /// </summary>
    private void SaveGrid(Grid grid)
    {
        var primary = displayMonitor.Attached.FirstOrDefault(screen => screen.Screen.Primary);
        if (primary?.Config != null)
        {
            layoutProvider.Save(layoutProvider.Get().WithScreen(primary.Config with { Grid = grid }));
        }
        appSettingsProvider.Save(appSettingsProvider.Get() with { Grid = grid });
    }

    // ---------- Grid mode ----------

    public GridModeOption[] GridModes =>
    [
        new(Locale.Settings_Advanced_GridMode_Manual, GridModeEnum.Manual),
        new(Locale.Settings_Advanced_GridMode_Virtual, GridModeEnum.Virtual),
        new(Locale.Settings_Advanced_GridMode_Free, GridModeEnum.Free)
    ];

    public GridModeOption GridMode
    {
        get => GridModes.FirstOrDefault(mode => mode.Value == appSettingsProvider.Get().Layout.GridMode)
               ?? GridModes[0];
        set
        {
            var settings = appSettingsProvider.Get();
            var newLayout = settings.Layout with { GridMode = value.Value };
            appSettingsProvider.Save(settings with { Layout = newLayout });
            this.RaisePropertyChanged(nameof(IsManualGrid));
        }
    }

    public bool IsManualGrid => appSettingsProvider.Get().Layout.GridMode == GridModeEnum.Manual;

    public int GridColumns
    {
        get => EffectiveGrid.Columns;
        set => SaveGrid(EffectiveGrid with { Columns = Math.Max(1, value) });
    }

    public int GridRows
    {
        get => EffectiveGrid.Rows;
        set => SaveGrid(EffectiveGrid with { Rows = Math.Max(1, value) });
    }

    public double GridCellPercent
    {
        get => EffectiveGrid.CellPercent;
        set => SaveGrid(EffectiveGrid with { CellPercent = Math.Max(0.01, value) });
    }

    public double GridXPercent
    {
        get => EffectiveGrid.XPercent;
        set => SaveGrid(EffectiveGrid with { XPercent = Math.Clamp(value, 0, 100) });
    }

    public double GridYPercent
    {
        get => EffectiveGrid.YPercent;
        set => SaveGrid(EffectiveGrid with { YPercent = Math.Clamp(value, 0, 100) });
    }

    // ---------- Sizing ----------

    public int Margin
    {
        get => appSettingsProvider.Get().Dimensions.Margin;
        set
        {
            var settings = appSettingsProvider.Get();
            appSettingsProvider.Save(settings with { Dimensions = settings.Dimensions with { Margin = value } });
        }
    }

    public int Radius
    {
        get => appSettingsProvider.Get().Dimensions.Radius;
        set
        {
            var settings = appSettingsProvider.Get();
            appSettingsProvider.Save(settings with { Dimensions = settings.Dimensions with { Radius = value } });
        }
    }

    public bool RadiusEnabled => !appSettingsProvider.Get().Theme.UseNativeFrame;

    public bool SnapPosition
    {
        get => appSettingsProvider.Get().Layout.SnapPosition;
        set
        {
            var settings = appSettingsProvider.Get();
            appSettingsProvider.Save(settings with { Layout = settings.Layout with { SnapPosition = value } });
        }
    }

    public bool SnapSize
    {
        get => appSettingsProvider.Get().Layout.SnapSize;
        set
        {
            var settings = appSettingsProvider.Get();
            appSettingsProvider.Save(settings with { Layout = settings.Layout with { SnapSize = value } });
        }
    }

    public bool LockPosition
    {
        get => appSettingsProvider.Get().Layout.LockPosition;
        set
        {
            var settings = appSettingsProvider.Get();
            appSettingsProvider.Save(settings with { Layout = settings.Layout with { LockPosition = value } });
        }
    }

    public bool LockSize
    {
        get => appSettingsProvider.Get().Layout.LockSize;
        set
        {
            var settings = appSettingsProvider.Get();
            appSettingsProvider.Save(settings with { Layout = settings.Layout with { LockSize = value } });
        }
    }

    // ---------- Network ----------

    public ProxyOption[] ProxyOptions =>
    [
        new(Locale.Settings_Advanced_Proxy_Direct, ""),
        new(Locale.Settings_Advanced_Proxy_System, "system"),
        new(Locale.Settings_Advanced_Proxy_Custom, "custom")
    ];

    /// <summary>
    /// Current proxy mode: Direct (""), System ("system") or Custom ("custom").
    /// </summary>
    public ProxyOption ProxyMode
    {
        get
        {
            var proxy = appSettingsProvider.Get().HttpProxy;
            if (string.IsNullOrEmpty(proxy)) return ProxyOptions[0];
            if (proxy == "system") return ProxyOptions[1];
            return ProxyOptions[2];
        }
        set
        {
            var settings = appSettingsProvider.Get();
            var proxy = value.Value switch
            {
                "system" => "system",
                "custom" => string.IsNullOrWhiteSpace(ProxyCustomUrl) ? "" : ProxyCustomUrl.Trim(),
                _ => ""
            };
            appSettingsProvider.Save(settings with { HttpProxy = proxy });
            this.RaisePropertyChanged(nameof(ProxyCustomUrl));
        }
    }

    /// <summary>
    /// Custom proxy URL (used when <see cref="ProxyMode"/> is Custom).
    /// </summary>
    public string ProxyCustomUrl
    {
        get
        {
            var proxy = appSettingsProvider.Get().HttpProxy;
            return proxy is null or "" or "system" ? "" : proxy;
        }
        set
        {
            var settings = appSettingsProvider.Get();
            appSettingsProvider.Save(settings with { HttpProxy = value });
        }
    }

    /// <summary>
    /// Custom update source URL; empty disables update checks.
    /// </summary>
    public string UpdateUrl
    {
        get => appSettingsProvider.Get().UpdateUrl ?? "";
        set
        {
            var settings = appSettingsProvider.Get();
            appSettingsProvider.Save(settings with { UpdateUrl = string.IsNullOrWhiteSpace(value) ? null : value.Trim() });
        }
    }
}