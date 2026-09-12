using System.Diagnostics;
using System.Reflection;
using uWidgets.Core.Models.Settings;

namespace Folders.Services;

/// <summary>
/// Safely invokes uWidgets LiquidGlassRenderer and LiquidGlassWallpaper via reflection.
/// Allows Folders widget to render liquid glass without direct project reference to SkiaSharp.
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
                    var rendererType = uWidgetsAsm.GetType("uWidgets.Services.LiquidGlassRenderer");
                    var wallpaperType = uWidgetsAsm.GetType("uWidgets.Services.LiquidGlassWallpaper");
                    if (rendererType != null && wallpaperType != null)
                    {
                        renderMethod = rendererType.GetMethod("Render", BindingFlags.Public | BindingFlags.Static);
                        getWallpaperMethod = wallpaperType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                        wallpaperInvalidatedEvent = wallpaperType.GetEvent("WallpaperInvalidated", BindingFlags.Public | BindingFlags.Static);
                        frameType = rendererType.GetNestedType("Frame");
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
