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
    public ThemeService(IAppSettingsProvider appSettingsProvider)
    {
        appSettingsProvider.DataChanging += (_, _, newSettings) => 
            Apply(newSettings.Theme);
    }
    
    private readonly StyleInclude transparentStyle = new(new Uri("avares://uWidgets/"))
    {
        Source = new Uri("avares://uWidgets/Styles/Transparent.axaml")
    };
    
    private readonly StyleInclude monochromeStyle = new(new Uri("avares://uWidgets/"))
    {
        Source = new Uri("avares://uWidgets/Styles/Monochrome.axaml")
    };
    
    public void Apply(Theme theme)
    {
        Application.Current!.RequestedThemeVariant = theme.DarkMode switch
        {
            null => ThemeVariant.Default,
            false => ThemeVariant.Light,
            _ => ThemeVariant.Dark,
        };
        
        Application.Current.Resources["BackgroundOpacity"] = theme.OpacityLevel;
        Application.Current.Resources["FontFamily"] = theme.FontFamily == "Inter"
            ? new FontFamily("avares://Avalonia.Fonts.Inter#Inter")
            : new FontFamily(theme.FontFamily);

        if (theme.AccentColor != null && Color.TryParse(theme.AccentColor, out var color))
        {
            Application.Current.Resources["SystemAccentColor"] = color;
            Application.Current.Resources["SystemAccentColorDark1"] = color;
            Application.Current.Resources["SystemAccentColorLight1"] = color;
        }
        
        // Surface material drives transparency: Solid is opaque (no translucency style);
        // Acrylic and LiquidGlass both render via Avalonia's own AcrylicBlur, which is
        // clipped per-card by SetWindowRgn (margin + corner radius). The Windows 11 DWM
        // system backdrop was tried for LiquidGlass but it ignores the window region and
        // fills the whole cell — the real gradient/refraction engine is a later native
        // (D3D/Win2D) phase.
        var translucent = theme.EffectiveSurface != SurfaceStyle.Solid;
        SwitchStyle(transparentStyle, translucent);
        SwitchStyle(monochromeStyle, theme.Monochrome);
    }

    private static void SwitchStyle(StyleInclude style, bool enable)
    {
        if (enable && !Application.Current!.Styles.Contains(style))
            Application.Current.Styles.Add(style);
        if (!enable && Application.Current!.Styles.Contains(style))
            Application.Current.Styles.Remove(style);
    }
}