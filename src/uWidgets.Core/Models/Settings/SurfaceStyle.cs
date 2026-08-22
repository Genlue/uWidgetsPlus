namespace uWidgets.Core.Models.Settings;

/// <summary>
/// The widget surface material. This is the primary distinction between the
/// two (currently) appearance presets; a third <see cref="LiquidGlass"/> mode
/// is planned and reserved here so it slots in without reworking the model.
/// </summary>
public enum SurfaceStyle
{
    /// <summary>毛玻璃 / frosted glass: translucent background + blur.</summary>
    Acrylic = 0,

    /// <summary>纯色: fully opaque background.</summary>
    Solid = 1,

    /// <summary>液态玻璃 / liquid glass: planned, reserved for a future build.</summary>
    LiquidGlass = 2
}
