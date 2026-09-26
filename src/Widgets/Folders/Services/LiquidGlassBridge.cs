using System.Diagnostics;
using System.Reflection;
using uWidgets.Core.Models.Settings;

namespace Folders.Services;

/// <summary>
/// Safely invokes uWidgets LiquidGlassDispatch and LiquidGlassWallpaper via reflection.
/// LiquidGlassDispatch routes to the optical model of the active material (液态玻璃 or
/// 新液态玻璃), so the Folders widget renders the right recipe without a direct project
/// reference to SkiaSharp.
/// </summary>
public static class LiquidGlassBridge
{
    private static MethodInfo? renderMethod;
    private static MethodInfo? getWallpaperMethod;
    private static Type? frameType;
    private static EventInfo? wallpaperInvalidatedEvent;
    private static bool initialized;
    private static readonly object initLock = new();

    public static void EnsureInitialized()
    {
        if (initialized) return;
        lock (initLock)
        {
            if (initialized) return;
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var uWidgetsAsm = assemblies.FirstOrDefault(a => a.GetName().Name == "uWidgets");
                if (uWidgetsAsm != null)
                {
                    var dispatchType = uWidgetsAsm.GetType("uWidgets.Services.LiquidGlassDispatch");
                    var wallpaperType = uWidgetsAsm.GetType("uWidgets.Services.LiquidGlassWallpaper");
                    if (dispatchType != null && wallpaperType != null)
                    {
                        renderMethod = dispatchType.GetMethod("Render", BindingFlags.Public | BindingFlags.Static);
                        getWallpaperMethod = wallpaperType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                        wallpaperInvalidatedEvent = wallpaperType.GetEvent("WallpaperInvalidated", BindingFlags.Public | BindingFlags.Static);
                        // The Frame record still lives on the classic renderer; both dispatch
                        // targets take it.
                        frameType = uWidgetsAsm.GetType("uWidgets.Services.LiquidGlassRenderer")?.GetNestedType("Frame");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LiquidGlassBridge initialization failed: {ex}");
            }
            finally
            {
                initialized = true;
            }
        }
    }

    public static void SubscribeWallpaperInvalidated(Action handler)
    {
        EnsureInitialized();
        if (wallpaperInvalidatedEvent != null)
        {
            try
            {
                wallpaperInvalidatedEvent.AddEventHandler(null, handler);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to subscribe to WallpaperInvalidated: {ex}");
            }
        }
    }

    /// <summary>
    /// Detach a handler added by <see cref="SubscribeWallpaperInvalidated"/>.
    /// <para>
    /// <c>WallpaperInvalidated</c> is a <b>static</b> event: a widget that subscribes it and never
    /// detaches stays rooted for the lifetime of the process, together with its visual tree and
    /// every icon bitmap it holds. Since the host re-creates widget content on each settings save,
    /// a missing detach leaks one whole widget per save.
    /// </para>
    /// </summary>
    public static void UnsubscribeWallpaperInvalidated(Action handler)
    {
        EnsureInitialized();
        if (wallpaperInvalidatedEvent == null) return;

        try
        {
            wallpaperInvalidatedEvent.RemoveEventHandler(null, handler);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to unsubscribe from WallpaperInvalidated: {ex}");
        }
    }

    public static bool IsAvailable
    {
        get
        {
            EnsureInitialized();
            return renderMethod != null && getWallpaperMethod != null && frameType != null;
        }
    }

    public static byte[]? Render(int width, int height, float scale, float radius,
        float desktopX, float desktopY, float desktopWidth, float desktopHeight,
        float screenX, float screenY, float screenWidth, float screenHeight,
        Theme theme, bool dark, bool settingsSurface = false, float pixelScale = 1)
    {
        if (!IsAvailable) return null;

        try
        {
            var wallpaper = getWallpaperMethod!.Invoke(null, null);
            if (wallpaper == null) return null;

            var frame = Activator.CreateInstance(frameType!,
                width, height, scale, radius,
                desktopX, desktopY, desktopWidth, desktopHeight,
                screenX, screenY, screenWidth, screenHeight,
                theme, dark, settingsSurface, pixelScale,
                0, 0);

            if (frame == null) return null;

            var result = renderMethod!.Invoke(null, new[] { frame, wallpaper }) as byte[];
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LiquidGlassBridge render error: {ex}");
            return null;
        }
    }
}
