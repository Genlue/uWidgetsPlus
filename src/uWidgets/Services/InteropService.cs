using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Controls;

namespace uWidgets.Services;

public class InteropService
{
    private const int SPI_GETDESKWALLPAPER = 0x0073;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int SystemParametersInfo(int uAction, int uParam, StringBuilder lpvParam, int fuWinIni);

    public static string GetWallpaperPath()
    {
        StringBuilder wallpaperPath = new StringBuilder(260);
        SystemParametersInfo(SPI_GETDESKWALLPAPER, wallpaperPath.Capacity, wallpaperPath, 0);
        return wallpaperPath.ToString();
    }
    public static void RemoveWindowFromAltTab(Window window)
    {
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int GWL_EXSTYLE = -20;

        var handle = window.TryGetPlatformHandle()?.Handle;
        
        if (handle == null) return;

        var exStyle = (int)GetWindowLong(handle.Value, GWL_EXSTYLE);

        exStyle |= WS_EX_TOOLWINDOW;
        SetWindowLong(handle.Value, GWL_EXSTYLE, exStyle);
    }

    /// <summary>
    /// Clip the native window to the widget card's rounded rectangle.
    /// <para>
    /// The widget window spans the whole grid cell, but the frosted-glass card
    /// only occupies the cell minus the margin, with rounded corners. Clipping
    /// the native window region makes the OS-side blur follow the card exactly,
    /// and makes the transparent margin area click-through (clicks outside the
    /// card fall through to the desktop instead of being captured by the widget).
    /// </para>
    /// </summary>
    /// <param name="window">The target window.</param>
    /// <param name="x">Region left edge (physical pixels, relative to the window).</param>
    /// <param name="y">Region top edge (physical pixels, relative to the window).</param>
    /// <param name="width">Region width (physical pixels).</param>
    /// <param name="height">Region height (physical pixels).</param>
    /// <param name="radius">Corner radius (physical pixels, typically <c>Dimensions.Radius</c>).</param>
    public static void SetWidgetRegion(Window window, int x, int y, int width, int height, int radius)
    {
        var handle = window.TryGetPlatformHandle()?.Handle;
        if (handle == null) return;

        width = Math.Max(1, width);
        height = Math.Max(1, height);
        radius = Math.Clamp(radius, 0, Math.Min(width, height) / 2);

        // Win32 GDI CreateRoundRectRgn treats right/bottom as exclusive.
        // x + width + 1 ensures the right and bottom border pixel columns (cardWidth - 1)
        // are included rather than truncated.
        var region = radius > 0
            ? CreateRoundRectRgn(x, y, x + width + 1, y + height + 1, radius * 2, radius * 2)
            : CreateRectRgn(x, y, x + width + 1, y + height + 1);
        if (region == IntPtr.Zero) return;

        // On success the system owns the region; only delete it on failure.
        if (SetWindowRgn(handle.Value, region, true) == 0)
            DeleteObject(region);
    }

    /// <summary>
    /// Set a custom non-rectangular window region (e.g. text glyphs) from horizontal scanline spans.
    /// Used by frameless widgets in OS-level AcrylicBlur mode so DWM applies real-time blur strictly
    /// inside the glyph shapes, while keeping the surrounding desktop visible and click-through.
    /// </summary>
    public static void SetWindowRegionFromSpans(Window window, IReadOnlyList<(int Left, int Top, int Right, int Bottom)> rects)
    {
        var handle = window.TryGetPlatformHandle()?.Handle;
        if (handle == null || rects == null || rects.Count == 0)
        {
            ClearWidgetRegion(window);
            return;
        }

        const int rectSize = 16;
        const int headerSize = 32;
        int count = rects.Count;
        int totalSize = headerSize + count * rectSize;
        byte[] buffer = new byte[totalSize];

        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        int offset = headerSize;
        for (int i = 0; i < count; i++)
        {
            var (l, t, r, b) = rects[i];
            if (l < minX) minX = l;
            if (t < minY) minY = t;
            if (r > maxX) maxX = r;
            if (b > maxY) maxY = b;

            BitConverter.GetBytes(l).CopyTo(buffer, offset + 0);
            BitConverter.GetBytes(t).CopyTo(buffer, offset + 4);
            BitConverter.GetBytes(r).CopyTo(buffer, offset + 8);
            BitConverter.GetBytes(b).CopyTo(buffer, offset + 12);
            offset += rectSize;
        }

        BitConverter.GetBytes(32).CopyTo(buffer, 0);       // dwSize
        BitConverter.GetBytes(1).CopyTo(buffer, 4);        // iType = RDH_RECTANGLES
        BitConverter.GetBytes((uint)count).CopyTo(buffer, 8); // nCount
        BitConverter.GetBytes((uint)(count * rectSize)).CopyTo(buffer, 12); // nRgnSize
        BitConverter.GetBytes(minX).CopyTo(buffer, 16);
        BitConverter.GetBytes(minY).CopyTo(buffer, 20);
        BitConverter.GetBytes(maxX).CopyTo(buffer, 24);
        BitConverter.GetBytes(maxY).CopyTo(buffer, 28);

        IntPtr rgn = ExtCreateRegion(IntPtr.Zero, (uint)totalSize, buffer);
        if (rgn != IntPtr.Zero)
        {
            if (SetWindowRgn(handle.Value, rgn, true) == 0)
                DeleteObject(rgn);
        }
    }

    /// <summary>
    /// Remove a previously applied window region (full-window rectangle again).
    /// </summary>
    public static void ClearWidgetRegion(Window window)
    {
        var handle = window.TryGetPlatformHandle()?.Handle;
        if (handle == null) return;
        SetWindowRgn(handle.Value, IntPtr.Zero, true);
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr ExtCreateRegion(IntPtr lpXform, uint nCount, byte[] lpRgnData);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRectRgn(int x1, int y1, int x2, int y2);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

    private static void SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        int error;
        IntPtr result;

        SetLastError(0);

        if (IntPtr.Size == 4)
        {
            var tempResult = IntSetWindowLong(hWnd, nIndex, IntPtrToInt32(dwNewLong));
            error = Marshal.GetLastWin32Error();
            result = new IntPtr(tempResult);
        }
        else
        {
            result = IntSetWindowLongPtr(hWnd, nIndex, dwNewLong);
            error = Marshal.GetLastWin32Error();
        }

        if (result == IntPtr.Zero && error != 0) throw new Win32Exception(error);
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr IntSetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int IntSetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private static int IntPtrToInt32(IntPtr intPtr)
    {
        return unchecked((int)intPtr.ToInt64());
    }

    [DllImport("kernel32.dll", EntryPoint = "SetLastError")]
    private static extern void SetLastError(int dwErrorCode);
}