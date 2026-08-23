namespace uWidgets.Core.Models.Settings;

/// <summary>
/// The widget surface material. This is the primary distinction between the
/// appearance presets; the remaining LiquidGlass mode is planned and reserved
/// so it slots in without reworking the model.
/// </summary>
public enum SurfaceStyle
{
    /// <summary>毛玻璃 / frosted glass: translucent background + blur.</summary>
    Acrylic = 0,

    /// <summary>纯色: fully opaque background.</summary>
    Solid = 1,

    /// <summary>液态玻璃 / liquid glass: planned, reserved for a future build.</summary>
    LiquidGlass = 2,

    /// <summary>
    /// Outlined frosted glass (描边毛玻璃), kept only for compatibility with
    /// configurations saved by earlier builds. Merged into <see cref="Acrylic"/>
    /// since 1.2.0: the outline is now an option of the frost theme itself,
    /// enabled by <see cref="Theme.OutlineWidth"/> &gt; 0 with
    /// <see cref="Theme.OutlineColor"/>.
    /// </summary>
    OutlinedAcrylic = 3
}
