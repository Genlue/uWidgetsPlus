using uWidgets.Core.Interfaces;
using uWidgets.Core.Models.Settings;

namespace uWidgets.Core.Services;

/// <inheritdoc cref="IAppSettingsProvider"/>
public class AppSettingsProvider() : JsonParser<AppSettings>(Const.AppSettingsFile), IAppSettingsProvider
{
    /// <inheritdoc />
    protected override AppSettings Normalize(AppSettings settings)
    {
        var surface = settings.Theme.EffectiveSurface;
        var theme = settings.Theme with { Surface = surface };
        var surfaceThemes = settings.SurfaceThemes != null
            ? new Dictionary<string, Theme>(settings.SurfaceThemes, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, Theme>(StringComparer.OrdinalIgnoreCase);

        if (!surfaceThemes.ContainsKey(surface.ToString()))
        {
            surfaceThemes[surface.ToString()] = theme;
        }

        var layout = settings.Layout;
        if ((int)layout.GridMode == 1)
        {
            layout = layout with { GridMode = GridMode.Manual };
        }

        return settings with
        {
            Grid = settings.Grid ?? Grid.Default,
            Theme = theme,
            SurfaceThemes = surfaceThemes,
            Layout = layout
        };
    }
}
