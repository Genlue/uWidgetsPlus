using System.Collections.Generic;
using System.Linq;
using ReactiveUI;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models.Settings;
using uWidgets.Locales;
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

public class AdvancedViewModel(IAppSettingsProvider appSettingsProvider) : ReactiveObject
{
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
        get => appSettingsProvider.Get().Grid?.Columns ?? Grid.Default.Columns;
        set
        {
            var settings = appSettingsProvider.Get();
            appSettingsProvider.Save(settings with { Grid = (settings.Grid ?? Grid.Default) with { Columns = value } });
        }
    }

    public int GridRows
    {
        get => appSettingsProvider.Get().Grid?.Rows ?? Grid.Default.Rows;
        set
        {
            var settings = appSettingsProvider.Get();
            appSettingsProvider.Save(settings with { Grid = (settings.Grid ?? Grid.Default) with { Rows = value } });
        }
    }

    public double GridCellPercent
    {
        get => appSettingsProvider.Get().Grid?.CellPercent ?? Grid.Default.CellPercent;
        set
        {
            var settings = appSettingsProvider.Get();
            appSettingsProvider.Save(settings with { Grid = (settings.Grid ?? Grid.Default) with { CellPercent = value } });
        }
    }

    public double GridXPercent
    {
        get => appSettingsProvider.Get().Grid?.XPercent ?? Grid.Default.XPercent;
        set
        {
            var settings = appSettingsProvider.Get();
            appSettingsProvider.Save(settings with { Grid = (settings.Grid ?? Grid.Default) with { XPercent = value } });
        }
    }

    public double GridYPercent
    {
        get => appSettingsProvider.Get().Grid?.YPercent ?? Grid.Default.YPercent;
        set
        {
            var settings = appSettingsProvider.Get();
            appSettingsProvider.Save(settings with { Grid = (settings.Grid ?? Grid.Default) with { YPercent = value } });
        }
    }

    // ---------- Sizing ----------

    public int Margin
    {
        get => appSettingsProvider.Get().Dimensions.Margin;
        set
        {
            var settings = appSettingsProvider.Get();
            var dimensions = settings.Dimensions with { Margin = value };
            var newSettings = settings with { Dimensions = dimensions };
            appSettingsProvider.Save(newSettings);
        }
    }
    
    public int Radius
    {
        get => appSettingsProvider.Get().Dimensions.Radius;
        set
        {
            var settings = appSettingsProvider.Get();
            var dimensions = settings.Dimensions with { Radius = value };
            var newSettings = settings with { Dimensions = dimensions };
            appSettingsProvider.Save(newSettings);
        }
    }

    /// <summary>
    /// Custom content scale (0.5× – 2×); 1.0 = content matches the design size for the current cell size.
    /// </summary>
    public double ContentScale
    {
        get => appSettingsProvider.Get().Dimensions.ContentScale;
        set
        {
            var settings = appSettingsProvider.Get();
            var dimensions = settings.Dimensions with { ContentScale = value };
            var newSettings = settings with { Dimensions = dimensions };
            appSettingsProvider.Save(newSettings);
        }
    }

    public bool RadiusEnabled => !appSettingsProvider.Get().Theme.UseNativeFrame;
    
    public bool SnapPosition
    {
        get => appSettingsProvider.Get().Layout.SnapPosition;
        set
        {
            var settings = appSettingsProvider.Get();
            var newLayout = settings.Layout with { SnapPosition = value };
            var newSettings = settings with { Layout = newLayout };
            appSettingsProvider.Save(newSettings);
        }
    }
    
    public bool SnapSize 
    {
        get => appSettingsProvider.Get().Layout.SnapSize;
        set
        {
            var settings = appSettingsProvider.Get();
            var newLayout = settings.Layout with { SnapSize = value };
            var newSettings = settings with { Layout = newLayout };
            appSettingsProvider.Save(newSettings);
        }
    }
    
    public bool LockPosition
    {
        get => appSettingsProvider.Get().Layout.LockPosition;
        set
        {
            var settings = appSettingsProvider.Get();
            var newLayout = settings.Layout with { LockPosition = value };
            var newSettings = settings with { Layout = newLayout };
            appSettingsProvider.Save(newSettings);
        }
    }
    
    public bool LockSize
    {
        get => appSettingsProvider.Get().Layout.LockSize;
        set
        {
            var settings = appSettingsProvider.Get();
            var newLayout = settings.Layout with { LockSize = value };
            var newSettings = settings with { Layout = newLayout };
            appSettingsProvider.Save(newSettings);
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
