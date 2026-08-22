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
            // mistaken for acrylic by the enum default. LiquidGlass is not yet a
            // distinct rendering engine (it renders like acrylic until the native
            // D3D/Win2D phase) — normalize it to acrylic so an experiment with the
            // placeholder preset degrades gracefully.
            Theme = settings.Theme with
            {
                Surface = settings.Theme.EffectiveSurface == SurfaceStyle.LiquidGlass
                    ? SurfaceStyle.Acrylic
                    : settings.Theme.EffectiveSurface,
                BlurLevel = settings.Theme.EffectiveBlur
            }
        };
}
