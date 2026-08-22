using uWidgets.Core.Interfaces;
using uWidgets.Core.Models.Settings;

namespace uWidgets.Core.Services;

/// <inheritdoc cref="IAppSettingsProvider"/>
public class AppSettingsProvider() : JsonParser<AppSettings>(Const.AppSettingsFile), IAppSettingsProvider
{
    /// <inheritdoc />
    protected override AppSettings Normalize(AppSettings settings) =>
        settings with
        {
            Grid = settings.Grid ?? Grid.Default,
            // Backfill the surface material from OpacityLevel for configurations
            // that predate the SurfaceStyle field so old "solid" setups aren't
            // mistaken for acrylic by the enum default.
            Theme = settings.Theme with { Surface = settings.Theme.EffectiveSurface }
        };
}
