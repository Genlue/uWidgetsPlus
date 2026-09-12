using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using Microsoft.Win32;
using SkiaSharp;

namespace uWidgets.Services;

/// <summary>
/// Resolves the wallpaper the glass samples. Primary source: a 1:1 capture of the
/// real composited desktop (PrintWindow on Progman), so the sampled background is
/// pixel-identical to what is actually behind the widget — including any layout
/// quirks introduced by taskbar replacements (myDockFinder), wallpaper engines or
/// the DWM. The static-file path (Windows wallpaper image + registry placement)
/// remains as a fallback when the capture is not available.
/// </summary>
public static class LiquidGlassWallpaper
{
    private const long CaptureTtlTicks = TimeSpan.TicksPerSecond * 2;

    private static readonly object Gate = new();
    private static string? cachedKey;
    private static WallpaperSnapshot? cached;
    private static long captureExpiresTicks;

    public static WallpaperSnapshot Get()
    {
        lock (Gate)
        {
            if (!OperatingSystem.IsWindows())
                return new WallpaperSnapshot(null, new SKColor(32, 38, 48));
            try
            {
                var live = CaptureOnce();
                if (live != null)
                {
                    return live;
                }
                return FromFileFallback();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                return cached ?? new WallpaperSnapshot(null, new SKColor(32, 38, 48));
            }
        }
    }

    /// <summary>
    /// Raised whenever the wallpaper is invalidated (system wallpaper change, display change, or manual refresh).
    /// </summary>
    public static event Action? WallpaperInvalidated;

    /// <summary>Force a fresh capture (e.g. the user pressed 刷新壁纸 or wallpaper changed).</summary>
    public static void Invalidate()
    {
        WallpaperSnapshot? old;
        lock (Gate)
        {
            captureExpiresTicks = 0;
            old = cached;
            cached = null;
            cachedKey = null;
        }

        if (old != null)
        {
            System.Threading.Tasks.Task.Delay(3000).ContinueWith(_ => old.Dispose());
        }

        try
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                WallpaperInvalidated?.Invoke();
            }
            else
            {
                Dispatcher.UIThread.Post(() => WallpaperInvalidated?.Invoke());
            }
        }
        catch
        {
            try { WallpaperInvalidated?.Invoke(); } catch { }
        }
    }

    private static WallpaperSnapshot? CaptureOnce()
    {
        if (captureExpiresTicks > DateTime.UtcNow.Ticks && cached != null && cached.LiveCapture)
            return cached;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var bmp = DesktopCapturer.CaptureBitmap();
            if (bmp == null)
            {
                continue;
            }
            var old = cached;
            cached = new WallpaperSnapshot(null, new SKColor(32, 38, 48), LiveCapture: true, CachedBitmap: bmp);
            captureExpiresTicks = DateTime.UtcNow.Ticks + CaptureTtlTicks;
            if (old != null && !ReferenceEquals(old, cached))
            {
                System.Threading.Tasks.Task.Delay(3000).ContinueWith(_ => old.Dispose());
            }
            return cached;
        }
        return null;
    }

    /// <summary>Static file based fallback (Windows wallpaper image + registry placement).</summary>
    private static WallpaperSnapshot FromFileFallback()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Windows\Themes\TranscodedWallpaper");
        if (!File.Exists(path)) path = InteropService.GetWallpaperPath();
        using var desktop = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
        using var colors = Registry.CurrentUser.OpenSubKey(@"Control Panel\Colors");
        var style = desktop?.GetValue("WallpaperStyle") as string ?? "10";
        var tile = desktop?.GetValue("TileWallpaper") as string == "1";
        var rgb = (colors?.GetValue("Background") as string ?? "32 38 48").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var background = rgb.Length == 3 && byte.TryParse(rgb[0], out var r) && byte.TryParse(rgb[1], out var g) && byte.TryParse(rgb[2], out var b)
            ? new SKColor(r, g, b) : new SKColor(32, 38, 48);
        var exists = File.Exists(path);
        var key = $"{path}|{(exists ? File.GetLastWriteTimeUtc(path).Ticks : 0)}|{style}|{tile}|{background}";
        if (cachedKey == key && cached != null && !cached.LiveCapture) return cached;
        var bytes = exists ? File.ReadAllBytes(path) : null;
        SKBitmap? bmp = null;
        if (bytes != null)
        {
            try
            {
                bmp = SKBitmap.Decode(bytes);
                bmp?.SetImmutable();
            }
            catch { }
        }
        var old = cached;
        cached = new WallpaperSnapshot(bytes, background, style, tile, CachedBitmap: bmp);
        cachedKey = key;
        if (old != null && !ReferenceEquals(old, cached))
        {
            System.Threading.Tasks.Task.Delay(3000).ContinueWith(_ => old.Dispose());
        }
        return cached;
    }
}

/// <summary>
/// Captures the virtual desktop wallpaper as Progman renders it — exactly what the
/// user sees behind the widgets, regardless of how the wallpaper layout was
/// computed (DWM, myDockFinder, wallpaper engines). Desktop icons and docks are not
/// part of Progman's own surface, so the capture is pure wallpaper.
/// </summary>
public static class DesktopCapturer
{
    private const uint PwRenderFullContent = 0x00000002;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr extraData);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder buffer, int maxCount);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint start, uint lines,
        IntPtr bits, ref BitmapInfo info, uint usage);

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr extraData);

    // SM_* virtual desktop metrics (physical pixels; the process is DPI aware).
    private const int SmXVirtualScreen = 76, SmYVirtualScreen = 77, SmCXVirtualScreen = 78, SmCYVirtualScreen = 79;

    public static SKBitmap? CaptureBitmap()
    {
        var left = GetSystemMetrics(SmXVirtualScreen);
        var top = GetSystemMetrics(SmYVirtualScreen);
        var width = GetSystemMetrics(SmCXVirtualScreen);
        var height = GetSystemMetrics(SmCYVirtualScreen);
        if (width <= 0 || height <= 0) return null;

        var hwnd = FindProgman();
        if (hwnd == IntPtr.Zero) return null;

        var screenDc = GetDC(IntPtr.Zero);
        var memDc = CreateCompatibleDC(screenDc);
        var bitmap = CreateCompatibleBitmap(screenDc, width, height);
        var previous = SelectObject(memDc, bitmap);
        try
        {
            var ok = PrintWindow(hwnd, memDc, PwRenderFullContent);
            if (!ok) return null;

            var skBitmap = ReadBitmap(memDc, bitmap, width, height);
            if (skBitmap == null) return null;

            if (!LooksLikeWallpaper(skBitmap))
            {
                skBitmap.Dispose();
                return null;
            }

            skBitmap.SetImmutable();
            return skBitmap;
        }
        finally
        {
            SelectObject(memDc, previous);
            DeleteObject(bitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    public static byte[] Capture()
    {
        using var bmp = CaptureBitmap();
        if (bmp == null) return [];
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    private static IntPtr FindProgman()
    {
        IntPtr result = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            var buffer = new System.Text.StringBuilder(256);
            GetClassName(hwnd, buffer, buffer.Capacity);
            if (buffer.ToString() == "Progman" && IsWindowVisible(hwnd))
            {
                result = hwnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }

    /// <summary>A valid wallpaper capture has meaningful content: not uniform, not black.</summary>
    private static bool LooksLikeWallpaper(SKBitmap bmp)
    {
        var width = bmp.Width;
        var height = bmp.Height;
        var seen = new HashSet<uint>();
        var stepX = Math.Max(1, width / 16);
        var stepY = Math.Max(1, height / 9);

        for (var y = stepY / 2; y < height; y += stepY)
        for (var x = stepX / 2; x < width; x += stepX)
        {
            var color = bmp.GetPixel(x, y);
            seen.Add(((uint)color.Red << 16) | ((uint)color.Green << 8) | color.Blue);
        }
        if (seen.Count < 3) return false;

        double sum = 0;
        foreach (var c in seen)
        {
            var r = (c >> 16) & 0xFF; var g = (c >> 8) & 0xFF; var b = c & 0xFF;
            sum += (299 * r + 587 * g + 114 * b) / 1000.0;
        }
        return sum / seen.Count > 4; // not a pure-black capture
    }

    private static SKBitmap? ReadBitmap(IntPtr memDc, IntPtr bitmap, int width, int height)
    {
        var info = CreateHeader(width, height);
        var skInfo = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        var skBitmap = new SKBitmap(skInfo);
        var pixels = skBitmap.GetPixels();
        if (pixels == IntPtr.Zero)
        {
            skBitmap.Dispose();
            return null;
        }

        if (GetDIBits(memDc, bitmap, 0, (uint)height, pixels, ref info, 0) != height)
        {
            skBitmap.Dispose();
            return null;
        }

        return skBitmap;
    }

    private static BitmapInfo CreateHeader(int width, int height) => new()
    {
        Header = new BitmapInfoHeader
        {
            Size = Marshal.SizeOf<BitmapInfoHeader>(),
            Width = width,
            Height = -height, // top-down rows
            Planes = 1,
            BitCount = 32,
            Compression = 0 // BI_RGB
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public int Colors;
    }
}
