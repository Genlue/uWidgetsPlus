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
// Alias: the TitleBarStyle property below would otherwise shadow the enum type name.
using TitleBarStyleEnum = uWidgets.Core.Models.Settings.TitleBarStyle;

namespace uWidgets.ViewModels;

public class AppearanceViewModel : ReactiveObject
{
    private readonly IAppSettingsProvider appSettingsProvider;

    public AppearanceViewModel(IAppSettingsProvider appSettingsProvider)
    {
        this.appSettingsProvider = appSettingsProvider;
        // Surface-dependent rows (glass outline) and the monochrome color picker
        // appear/disappear when the settings change.
        appSettingsProvider.DataChanged += (_, _, _) =>
        {
            this.RaisePropertyChanged(nameof(ShowGlassSettings));
            this.RaisePropertyChanged(nameof(ShowMonochromeVariant));
            this.RaisePropertyChanged(nameof(ShowTitleBarSize));
            this.RaisePropertyChanged(nameof(TitleBarSize));
        };
        // Built once; keyed off the fixed presets, so every install shows exactly two.
        Themes = SurfaceTemplates.Select(theme => new ThemeButton(appSettingsProvider, theme)).ToArray();
    }

    /// <summary>
    /// The surface presets — exactly two (毛玻璃 / 纯色). The outline (描边) is an
    /// option of the frost theme itself, not a separate surface. A liquid-glass
    /// preset may be added here later — it is already reserved via <see cref="SurfaceStyle"/>.
    /// </summary>
    private static readonly Theme[] SurfaceTemplates =
    [
        new(DarkMode: null, AccentColor: null, OpacityLevel: 0.4, Monochrome: true, UseNativeFrame: false, FontFamily: "Inter", Surface: SurfaceStyle.Acrylic),
        new(DarkMode: null, AccentColor: null, OpacityLevel: 1.0, Monochrome: true, UseNativeFrame: false, FontFamily: "Inter", Surface: SurfaceStyle.Solid)
    ];

    public ThemeButton[] Themes { get; }

    /// <summary>
    /// True when the theme is a glass surface — the blur-strength and outline
    /// settings are hidden (and ignored) for the solid preset.
    /// </summary>
    public bool ShowGlassSettings => appSettingsProvider.Get().Theme.IsGlass;
    
    public DarkModeViewModel[] DarkModes =>
    [
        new DarkModeViewModel(Locale.Settings_Appearance_DarkMode_False, false),
        new DarkModeViewModel(Locale.Settings_Appearance_DarkMode_True, true),
        new DarkModeViewModel(Locale.Settings_Appearance_DarkMode_Null, null),
        new DarkModeViewModel(Locale.Settings_Appearance_DarkMode_Auto, null, Auto: true)
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
        get
        {
            var theme = appSettingsProvider.Get().Theme;
            return DarkModes.FirstOrDefault(mode => mode.Auto == theme.AutoTheme
                && (theme.AutoTheme || mode.Value == theme.DarkMode));
        }
        set
        {
            var settings = appSettingsProvider.Get();
            var newTheme = settings.Theme with
            {
                DarkMode = value?.Value,
                AutoTheme = value?.Auto ?? false
            };
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

    /// <summary>
    /// Highlight-ring color of the outlined-glass theme (RGBA HEX, alpha preserved).
    /// </summary>
    public Color OutlineColor
    {
        get => Color.TryParse(
            appSettingsProvider.Get().Theme.EffectiveOutlineColor, out var color) ? color : Colors.White;
        set
        {
            var settings = appSettingsProvider.Get();
            var newTheme = settings.Theme with { OutlineColor = value.ToString() };
            var newSettings = settings with { Theme = newTheme };
            appSettingsProvider.Save(newSettings);
        }
    }

    /// <summary>
    /// Highlight-ring thickness of the outlined-glass theme in DIPs; 0 hides the ring.
    /// </summary>
    public double OutlineWidth
    {
        get => appSettingsProvider.Get().Theme.OutlineWidth;
        set
        {
            var settings = appSettingsProvider.Get();
            var newTheme = settings.Theme with { OutlineWidth = Math.Clamp(value, 0, 6) };
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
            this.RaisePropertyChanged(nameof(ShowMonochromeVariant));
        }
    }

    /// <summary>
    /// The monochrome color source options (黑白 / 强调色).
    /// </summary>
    public MonochromeVariantViewModel[] MonochromeVariants { get; } =
    [
        new(Locale.Settings_Appearance_Monochrome_Variant_BlackWhite, MonochromeStyle.BlackWhite),
        new(Locale.Settings_Appearance_Monochrome_Variant_Accent, MonochromeStyle.Accent)
    ];

    /// <summary>True when the monochrome color source combo is shown (monochrome enabled).</summary>
    public bool ShowMonochromeVariant => Monochrome;

    public MonochromeVariantViewModel MonochromeVariant
    {
        get => MonochromeVariants.First(variant =>
            variant.Value == appSettingsProvider.Get().Theme.EffectiveMonochromeVariant);
        set
        {
            if (value == null) return;
            var settings = appSettingsProvider.Get();
            var newTheme = settings.Theme with { MonochromeVariant = value.Value };
            var newSettings = settings with { Theme = newTheme };
            appSettingsProvider.Save(newSettings);
        }
    }

    /// <summary>
    /// Widget card background color in dark mode (HEX, alpha is the global opacity slider's job).
    /// Applies to both 毛玻璃 and 纯色 surfaces.
    /// </summary>
    public Color SolidBackgroundDark
    {
        get => ParseColor(appSettingsProvider.Get().Theme.EffectiveSolidBackgroundDark, Theme.DefaultSolidBackgroundDark);
        set => SaveSolidBackground(SolidBackgroundDark: ToHex(value));
    }

    /// <summary>
    /// Widget card background color in light mode (HEX, alpha is the global opacity slider's job).
    /// Applies to both 毛玻璃 and 纯色 surfaces.
    /// </summary>
    public Color SolidBackgroundLight
    {
        get => ParseColor(appSettingsProvider.Get().Theme.EffectiveSolidBackgroundLight, Theme.DefaultSolidBackgroundLight);
        set => SaveSolidBackground(SolidBackgroundLight: ToHex(value));
    }

    /// <summary>
    /// Apply the currently selected dark-mode color to the light-mode color.
    /// </summary>
    public void ApplySolidToLight()
    {
        SaveSolidBackground(SolidBackgroundLight: ToHex(SolidBackgroundDark));
        this.RaisePropertyChanged(nameof(SolidBackgroundLight));
    }

    /// <summary>
    /// Apply the currently selected light-mode color to the dark-mode color.
    /// </summary>
    public void ApplySolidToDark()
    {
        SaveSolidBackground(SolidBackgroundDark: ToHex(SolidBackgroundLight));
        this.RaisePropertyChanged(nameof(SolidBackgroundDark));
    }

    /// <summary>
    /// Store colors as a plain "#AARRGGBB" string — <see cref="Color.ToString"/>
    /// may return a named color (e.g. "White") that doesn't round-trip through
    /// the settings JSON in a stable way.
    /// </summary>
    private static string ToHex(Color color) =>
        $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    private void SaveSolidBackground(string? SolidBackgroundDark = null, string? SolidBackgroundLight = null)
    {
        var settings = appSettingsProvider.Get();
        var theme = settings.Theme with
        {
            SolidBackgroundDark = SolidBackgroundDark ?? settings.Theme.SolidBackgroundDark,
            SolidBackgroundLight = SolidBackgroundLight ?? settings.Theme.SolidBackgroundLight
        };
        appSettingsProvider.Save(settings with { Theme = theme });
    }

    private static Color ParseColor(string hex, string fallbackHex) =>
        Color.TryParse(hex, out var color) ? color : Color.Parse(fallbackHex);
    
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

    /// <summary>
    /// Title bar style options of the settings window (native system buttons
    /// or macOS traffic lights).
    /// </summary>
    public TitleBarStyleViewModel[] TitleBarStyles =>
    [
        new(Locale.Settings_Advanced_TitleBarStyle_Native, TitleBarStyleEnum.Native),
        new(Locale.Settings_Advanced_TitleBarStyle_TrafficLights, TitleBarStyleEnum.TrafficLights)
    ];

    /// <summary>
    /// The selected title bar style. Takes effect immediately (the window
    /// chrome is reconfigured on save).
    /// </summary>
    public TitleBarStyleViewModel TitleBarStyle
    {
        get => TitleBarStyles.First(style =>
            style.Value == appSettingsProvider.Get().EffectiveTitleBarStyle);
        set
        {
            if (value == null) return;
            appSettingsProvider.Save(appSettingsProvider.Get() with { TitleBarStyle = value.Value });
        }
    }

    /// <summary>True when the traffic-light title bar is active (its size row is shown).</summary>
    public bool ShowTitleBarSize =>
        appSettingsProvider.Get().EffectiveTitleBarStyle == TitleBarStyleEnum.TrafficLights;

    /// <summary>
    /// Traffic light size options (diameter in DIPs).
    /// </summary>
    public TitleBarSizeViewModel[] TitleBarSizes => TitleBarSizeViewModel.Options;

    /// <summary>
    /// The traffic light diameter in DIPs (proportional scaling, 12px = macOS standard).
    /// </summary>
    public TitleBarSizeViewModel TitleBarSize
    {
        get => TitleBarSizes.First(option => Math.Abs(option.Value - appSettingsProvider.Get().EffectiveTitleBarSize) < 0.001);
        set
        {
            if (value == null) return;
            appSettingsProvider.Save(appSettingsProvider.Get() with { TitleBarSize = value.Value });
        }
    }

}