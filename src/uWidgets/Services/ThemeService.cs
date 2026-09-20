using System;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models.Settings;

namespace uWidgets.Services;

public class ThemeService : IThemeService
{
    private readonly WallpaperThemeService wallpaperThemeService;

    public ThemeService(IAppSettingsProvider appSettingsProvider, WallpaperThemeService wallpaperThemeService)
    {
        this.wallpaperThemeService = wallpaperThemeService;
        appSettingsProvider.DataChanging += (_, _, newSettings) => 
            Apply(newSettings.Theme);
        // Wallpaper changed while in auto mode: re-resolve the variant.
        this.wallpaperThemeService.DarkFlagChanged += _ =>
        {
            var settings = appSettingsProvider.Get();
            if (settings.Theme.AutoTheme) Apply(settings.Theme);
        };
    }
    
    private readonly StyleInclude transparentStyle = new(new Uri("avares://uWidgets/"))
    {
        Source = new Uri("avares://uWidgets/Styles/Transparent.axaml")
    };
    
    private readonly StyleInclude solidStyle = new(new Uri("avares://uWidgets/"))
    {
        Source = new Uri("avares://uWidgets/Styles/Solid.axaml")
    };

    private readonly StyleInclude liquidGlassStyle = new(new Uri("avares://uWidgets/"))
    {
        Source = new Uri("avares://uWidgets/Styles/LiquidGlass.axaml")
    };

    private readonly StyleInclude colorfulStyle = new(new Uri("avares://uWidgets/"))
    {
        Source = new Uri("avares://uWidgets/Styles/Colorful.axaml")
    };
    
    private readonly StyleInclude monochromeStyle = new(new Uri("avares://uWidgets/"))
    {
        Source = new Uri("avares://uWidgets/Styles/Monochrome.axaml")
    };

    private readonly StyleInclude monochromeBlackWhiteStyle = new(new Uri("avares://uWidgets/"))
    {
        Source = new Uri("avares://uWidgets/Styles/MonochromeBlackWhite.axaml")
    };

    /// <summary>
    /// 强调色 accent foreground (widget titles / accent icons), always loaded.
    /// The monochrome styles are appended AFTER it, so they can unify this
    /// accent with the text color when monochrome is enabled.
    /// </summary>
    private readonly StyleInclude accentStyle = new(new Uri("avares://uWidgets/"))
    {
        Source = new Uri("avares://uWidgets/Styles/Accent.axaml")
    };
    
    public void Apply(Theme theme)
    {
        // Auto mode resolves light/dark from the wallpaper brightness; otherwise
        // the explicit flag decides (null = follow the system).
        var darkMode = theme.AutoTheme ? wallpaperThemeService.IsWallpaperDark() : theme.DarkMode;
        Application.Current!.RequestedThemeVariant = darkMode switch
        {
            null => ThemeVariant.Default,
            false => ThemeVariant.Light,
            _ => ThemeVariant.Dark,
        };
        
        Application.Current.Resources["FontFamily"] = theme.FontFamily == "Inter"
            ? new FontFamily("avares://Avalonia.Fonts.Inter#Inter")
            : new FontFamily(theme.FontFamily);

        // OS-level acrylic (the Transparent style sets the AcrylicBlur hint on the
        // windows): the desktop composer samples the live desktop every frame, so
        // dynamic wallpapers stay live behind the widgets. OpacityLevel is the
        // coating alpha. In Colorful mode, opacity is strictly 1.0 (opaque cards).
        Application.Current.Resources["BackgroundOpacity"] = theme.IsColorful ? 1.0 : theme.OpacityLevel;

        if (theme.IsColorful)
        {
            var appleBlue = Color.Parse("#007AFF");
            Application.Current.Resources["SystemAccentColor"] = appleBlue;
            Application.Current.Resources["SystemAccentColorDark1"] = appleBlue;
            Application.Current.Resources["SystemAccentColorLight1"] = Color.Parse("#0A84FF");
        }
        else if (theme.AccentColor != null && Color.TryParse(theme.AccentColor, out var color))
        {
            Application.Current.Resources["SystemAccentColor"] = color;
            Application.Current.Resources["SystemAccentColorDark1"] = color;
            Application.Current.Resources["SystemAccentColorLight1"] = color;
        }

        // 纯色 surface: the card color (per dark/light variant) and the coating
        // opacity — Solid.axaml's WidgetBackground brush picks these up.
        // In Colorful mode, fixed authentic Apple card backgrounds are strictly enforced.
        Application.Current.Resources["SolidBackgroundDark"] = theme.IsColorful
            ? Color.Parse("#1C1C1E")
            : ParseColor(theme.EffectiveSolidBackgroundDark, Theme.DefaultSolidBackgroundDark);
        Application.Current.Resources["SolidBackgroundLight"] = theme.IsColorful
            ? Color.Parse("#FFFFFF")
            : ParseColor(theme.EffectiveSolidBackgroundLight, Theme.DefaultSolidBackgroundLight);
        
        // Surface material drives both the background style and the transparency
        // hint: Acrylic/OutlinedAcrylic → OS-level live blur, Solid → per-pixel
        // transparency so the opacity slider actually blends with the desktop.
        // Colorful (macOS) uses live OS acrylic blur with rich system semantic colors.
        // Always on base accent fallback (generic icons / title fallback)
        SwitchStyle(accentStyle, true);

        // Monochrome color source: 黑白 = black in light / white in dark mode
        // (both text AND accent colors), 强调色 = accent-based (the historic
        // Monochrome.axaml dictionaries — accent stays accent, text becomes accent).
        // In Colorful mode, monochrome is strictly disabled.
        var monochrome = !theme.IsColorful && theme.Monochrome && theme.EffectiveMonochromeVariant == MonochromeStyle.BlackWhite;
        SwitchStyle(monochromeBlackWhiteStyle, monochrome);
        SwitchStyle(monochromeStyle, !theme.IsColorful && theme.Monochrome && !monochrome);

        // Surface material background styles
        SwitchStyle(transparentStyle, theme.UsesNativeBlur && !theme.IsColorful);
        SwitchStyle(solidStyle, !theme.IsGlass && !theme.IsColorful);
        SwitchStyle(liquidGlassStyle, theme.UsesRenderedGlass);

        // Colorful (macOS) uses live OS acrylic blur with rich Apple HIG system semantic colors.
        // Loaded after accentStyle so its vibrant palette (Red calendar, Orange clock second hand,
        // Blue/Purple/Orange monitor rings, etc.) takes precedence over single-color accent fallbacks.
        SwitchStyle(colorfulStyle, theme.IsColorful);
    }

    private static Color ParseColor(string hex, string fallbackHex) =>
        Color.TryParse(hex, out var color) ? color : Color.Parse(fallbackHex);

    private static void SwitchStyle(StyleInclude style, bool enable)
    {
        if (enable && !Application.Current!.Styles.Contains(style))
            Application.Current.Styles.Add(style);
        if (!enable && Application.Current!.Styles.Contains(style))
            Application.Current.Styles.Remove(style);
    }
}
