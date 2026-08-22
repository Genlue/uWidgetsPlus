using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using ReactiveUI;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models.Settings;
using uWidgets.Locales;
using uWidgets.Services;
using uWidgets.Views.Controls;

namespace uWidgets.ViewModels;

public class AppearanceViewModel(IAppSettingsProvider appSettingsProvider) : ReactiveObject
{
    /// <summary>
    /// The surface presets: 毛玻璃 (acrylic) / 纯色 (solid). The old eight templates
    /// were only corner-radius / dark-light / font variants of these two materials
    /// and are now controlled by their own settings.
    /// <para>
    /// <see cref="SurfaceStyle.LiquidGlass"/> is reserved for the future native
    /// D3D/Win2D glass engine; the Windows 11 DWM system backdrop was evaluated and
    /// rejected because it ignores the per-card window-region clipping (margins and
    /// corner radius), so it is not offered in the UI yet.
    /// </para>
    /// </summary>
    private static readonly Theme[] SurfaceTemplates =
    [
        new(DarkMode: null, AccentColor: null, OpacityLevel: 0.4, Monochrome: true, UseNativeFrame: false, FontFamily: "Inter", Surface: SurfaceStyle.Acrylic),
        new(DarkMode: null, AccentColor: null, OpacityLevel: 1.0, Monochrome: true, UseNativeFrame: false, FontFamily: "Inter", Surface: SurfaceStyle.Solid)
    ];

    public ThemeButton[] Themes { get; } =
        // Built once; keyed off the fixed presets, so every install shows exactly two.
        SurfaceTemplates.Select(theme => new ThemeButton(appSettingsProvider, theme)).ToArray();
    
    public DarkModeViewModel[] DarkModes =>
    [
        new DarkModeViewModel(Locale.Settings_Appearance_DarkMode_False, false),
        new DarkModeViewModel(Locale.Settings_Appearance_DarkMode_True, true),
        new DarkModeViewModel(Locale.Settings_Appearance_DarkMode_Null, null)
    ];

    public AccentColorViewModel[] AccentComboboxItems =>
    [
        new AccentColorViewModel(Locale.Settings_Appearance_AccentColor_Null, null),
        new AccentColorViewModel(Locale.Settings_Appearance_AccentColor_Manual, "#3376CD")
    ];

    public bool ShowColorPalette => appSettingsProvider.Get().Theme.AccentColor != null;
    
    public AccentColorViewModel AccentMode
    {
        get => appSettingsProvider.Get().Theme.AccentColor == null ? AccentComboboxItems[0] : AccentComboboxItems[1];
        set
        {
            var settings = appSettingsProvider.Get();
            var newTheme = settings.Theme with { AccentColor = value.Value };
            var newSettings = settings with { Theme = newTheme };
            appSettingsProvider.Save(newSettings);
            this.RaisePropertyChanged(nameof(ShowColorPalette));
        }
    }

    public Color AccentColor
    {
        get => Color.TryParse(appSettingsProvider.Get().Theme.AccentColor, out var color) ? color : Colors.DodgerBlue;
        set
        {
            var settings = appSettingsProvider.Get();
            var newTheme = settings.Theme with { AccentColor = value.ToString() };
            var newSettings = settings with { Theme = newTheme };
            appSettingsProvider.Save(newSettings);
        }
    }

    public DarkModeViewModel? DarkMode
    {
        get => DarkModes.FirstOrDefault(theme => theme.Value == appSettingsProvider.Get().Theme.DarkMode);
        set
        {
            var settings = appSettingsProvider.Get();
            var newTheme = settings.Theme with { DarkMode = value?.Value };
            var newSettings = settings with { Theme = newTheme };
            appSettingsProvider.Save(newSettings);
        }
    }
    
    public double OpacityLevel
    {
        get => appSettingsProvider.Get().Theme.OpacityLevel;
        set
        {
            var settings = appSettingsProvider.Get();
            var newTheme = settings.Theme with { OpacityLevel = value };
            var newSettings = settings with { Theme = newTheme };
            appSettingsProvider.Save(newSettings);
        }
    }
    

    
    public bool Monochrome
    {
        get => appSettingsProvider.Get().Theme.Monochrome;
        set
        {
            var settings = appSettingsProvider.Get();
            var newTheme = settings.Theme with { Monochrome = value };
            var newSettings = settings with { Theme = newTheme };
            appSettingsProvider.Save(newSettings);
        }
    }
    
    /// <summary>
    /// Every font available: all fonts installed on the system, plus the bundled Inter font.
    /// </summary>
    public IReadOnlyList<string> AllFonts { get; } = SystemFonts.GetFonts().ToArray();

    private string fontSearchText = "";
    public string FontSearchText
    {
        get => fontSearchText;
        set
        {
            if (fontSearchText == value) return;
            fontSearchText = value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(FilteredFonts));
            this.RaisePropertyChanged(nameof(NoFontResults));
        }
    }

    /// <summary>
    /// Fonts filtered by <see cref="FontSearchText"/>.
    /// </summary>
    public IReadOnlyList<string> FilteredFonts =>
        string.IsNullOrWhiteSpace(FontSearchText)
            ? AllFonts
            : AllFonts
                .Where(font => font.Contains(FontSearchText.Trim(), System.StringComparison.OrdinalIgnoreCase))
                .ToArray();

    /// <summary>
    /// True when the font search matched nothing.
    /// </summary>
    public bool NoFontResults => FilteredFonts.Count == 0;

    public string Font
    {
        get => appSettingsProvider.Get().Theme.FontFamily;
        set
        {
            var settings = appSettingsProvider.Get();
            var theme = settings.Theme with { FontFamily = value };
            var newSettings = settings with { Theme = theme };
            appSettingsProvider.Save(newSettings);
        }
    }

    public bool UseNativeFrame
    {
        get => appSettingsProvider.Get().Theme.UseNativeFrame;
        set
        {
            var settings = appSettingsProvider.Get();
            var theme = settings.Theme with { UseNativeFrame = value };
            var newSettings = settings with { Theme = theme };
            appSettingsProvider.Save(newSettings);
        }        
    }

}