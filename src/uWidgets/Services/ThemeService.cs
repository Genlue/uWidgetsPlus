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
    
    private readonly StyleInclude liquidGlassStyle = new(new Uri("avares://uWidgets/"))
    {
        Source = new Uri("avares://uWidgets/Styles/LiquidGlass.axaml")
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
        
        // Surface material drives transparency: Solid is opaque (no translucency style),
        // Acrylic uses Avalonia's own AcrylicBlur, LiquidGlass defers to the Windows 11
        // system gradient-blur backdrop (InteropService.SetLiquidGlassBackdrop).
        SwitchStyle(transparentStyle, theme.EffectiveSurface == SurfaceStyle.Acrylic);
        SwitchStyle(liquidGlassStyle, theme.EffectiveSurface == SurfaceStyle.LiquidGlass);
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