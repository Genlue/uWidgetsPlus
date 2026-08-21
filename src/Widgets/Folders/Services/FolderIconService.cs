using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Folders.Services;

/// <summary>
/// Extracts the associated system icon of a file/folder/shortcut as an Avalonia bitmap.
/// Uses IShellItemImageFactory first (same source as Explorer), then falls back to the shell image list.
/// </summary>
public static class FolderIconService
{
    private static readonly Dictionary<string, (Bitmap? Icon, DateTime Stamp)> iconCache = new();
    private static readonly object cacheLock = new();
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

    [DllImport("gdi32.dll")]
    private static extern IntPtr GetObject(IntPtr h, int c, out BITMAP b);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr h);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint cLines, IntPtr lpvBits, ref BITMAPINFO lpbmi, uint usage);

    /// <summary>
    /// Get the icon of <paramref name="path"/> as a 32-bit BGRA bitmap.
    /// Uses IShellItemImageFactory first (same source as Explorer), then falls back to SHGetFileInfo.
    /// </summary>
    public static Bitmap? GetIcon(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var stamp = File.GetLastWriteTimeUtc(path);
        lock (cacheLock)
        {
            if (iconCache.TryGetValue(path, out var cached) && cached.Stamp == stamp)
                return cached.Icon;
        }

        var icon = GetIconFromIcoLink(path)
            ?? GetIconFromShellItemImageFactory(path)
            ?? GetIconFromShGetFileInfo(path);

        lock (cacheLock)
        {
            iconCache[path] = (icon, stamp);
        }
        return icon;
    }

    /// <summary>
    /// Decode the icon directly from an .ico file, or from the .ico file a .lnk points to.
    /// This keeps the icon the user has set on the shortcut itself, at full resolution.
    /// </summary>
    private static Bitmap? GetIconFromIcoLink(string path)
    {
        try
        {
            if (path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                return GetIconFromIcoFile(path);

            if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var iconPath = ResolveLnkIconLocation(path);
                if (!string.IsNullOrEmpty(iconPath)
                    && iconPath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)
                    && File.Exists(iconPath))
                    return GetIconFromIcoFile(iconPath);
            }
        }
        catch
        {
            return null;
        }
        return null;
    }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport, Guid("0000010B-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder ppszFileName);
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLinkCoClass { }

    private static string? ResolveLnkIconLocation(string lnkPath)
    {
        try
        {
            var link = (IShellLinkW)new ShellLinkCoClass();
            ((IPersistFile)link).Load(lnkPath, 0);
            var sb = new System.Text.StringBuilder(1024);
            link.GetIconLocation(sb, 1024, out _);
            return sb.Length == 0 ? null : sb.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parse an .ico file and decode its largest frame. Supports PNG-compressed
    /// frames and classic 32/24/8bpp DIB frames (with AND mask fallback).
    /// </summary>
    private static Bitmap? GetIconFromIcoFile(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 6) return null;
        var count = BitConverter.ToUInt16(bytes, 4);
        if (count == 0 || count > 200) return null;

        var best = -1;
        var bestScore = 0;
        for (var i = 0; i < count; i++)
        {
            var off = 6 + i * 16;
            if (off + 16 > bytes.Length) break;
            var w = bytes[off] == 0 ? 256 : bytes[off];
            var h = bytes[off + 1] == 0 ? 256 : bytes[off + 1];
            var score = Math.Min(w, h);
            if (score > bestScore) { bestScore = score; best = i; }
        }
        if (best < 0) return null;

        var entryOff = 6 + best * 16;
        var dataLen = BitConverter.ToInt32(bytes, entryOff + 8);
        var dataOff = BitConverter.ToInt32(bytes, entryOff + 12);
        if (dataLen <= 0 || dataOff < 0 || dataOff + dataLen > bytes.Length) return null;

        var data = new byte[dataLen];
        Array.Copy(bytes, dataOff, data, 0, dataLen);

        if (data.Length > 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
        {
            using var ms = new MemoryStream(data);
            return new Bitmap(ms);
        }
        return GetIconFromIcoBmpFrame(data);
    }

    private static Bitmap? GetIconFromIcoBmpFrame(byte[] data)
    {
        if (data.Length < 40) return null;
        var width = BitConverter.ToInt32(data, 4);
        var biHeight = BitConverter.ToInt32(data, 8);
        var bitCount = BitConverter.ToUInt16(data, 14);
        if (width <= 0 || width > 512) return null;

        var topDown = biHeight < 0;
        var height = topDown ? Math.Abs(biHeight) : Math.Abs(biHeight) / 2;
        if (height <= 0 || height > 512) return null;

        var paletteOffset = 40;
        var colors = 0;
        var bytesPerPixel = bitCount switch { 32 => 4, 24 => 3, 8 => 1, _ => 0 };
        if (bytesPerPixel == 0) return null;
        if (bitCount == 8)
        {
            colors = BitConverter.ToInt32(data, 32);
            if (colors == 0) colors = 256;
            paletteOffset = 40 + colors * 4;
        }
        if (paletteOffset + width * height * bytesPerPixel > data.Length) return null;

        var pixels = new byte[width * height * 4];
        var palette = new byte[colors * 4];

        // apply AND mask only when the frame has no usable alpha
        var useMask = bitCount != 32;
        var maskStride = ((width + 31) / 32) * 4;
        var maskOffset = paletteOffset + width * height * bytesPerPixel;
        var mask = (maskOffset + maskStride * height <= data.Length) ? data : null;

        for (var y = 0; y < height; y++)
        {
            var srcRow = topDown ? y : height - 1 - y;
            var dstRow = y;
            for (var x = 0; x < width; x++)
            {
                var src = paletteOffset + (srcRow * width + x) * bytesPerPixel;
                var dst = (dstRow * width + x) * 4;
                switch (bitCount)
                {
                    case 32:
                        pixels[dst] = data[src];
                        pixels[dst + 1] = data[src + 1];
                        pixels[dst + 2] = data[src + 2];
                        pixels[dst + 3] = data[src + 3];
                        break;
                    case 24:
                        pixels[dst] = data[src];
                        pixels[dst + 1] = data[src + 1];
                        pixels[dst + 2] = data[src + 2];
                        pixels[dst + 3] = 255;
                        break;
                    case 8:
                        Array.Copy(data, paletteOffset, palette, 0, palette.Length);
                        var idx = data[src] * 4;
                        pixels[dst] = palette[idx];
                        pixels[dst + 1] = palette[idx + 1];
                        pixels[dst + 2] = palette[idx + 2];
                        pixels[dst + 3] = 255;
                        break;
                }
            }
        }

        if (mask != null)
        {
            if (bitCount == 32 && HasAlpha(pixels)) useMask = false;
            if (useMask)
            {
                for (var y = 0; y < height; y++)
                {
                    var srcRow = topDown ? y : height - 1 - y;
                    for (var x = 0; x < width; x++)
                    {
                        var bitIndex = srcRow * maskStride + x / 8;
                        if (bitIndex >= mask.Length) continue;
                        var isMasked = (mask[bitIndex] & (0x80 >> (x % 8))) != 0;
                        pixels[(y * width + x) * 4 + 3] = isMasked ? (byte)0 : (byte)255;
                    }
                }
            }
        }

        return PixelsToWriteableBitmap(pixels, width, height);
    }

    private static bool HasAlpha(byte[] pixels)
    {
        for (var i = 3; i < pixels.Length; i += 4)
            if (pixels[i] != 0) return true;
        return false;
    }

    private static Bitmap PixelsToWriteableBitmap(byte[] pixels, int width, int height)
    {
        var writeable = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using (var framebuffer = writeable.Lock())
        {
            unsafe
            {
                var dst = new Span<byte>((void*)framebuffer.Address, pixels.Length);
                for (var i = 0; i < pixels.Length; i += 4)
                {
                    var b = pixels[i];
                    var g = pixels[i + 1];
                    var r = pixels[i + 2];
                    var a = pixels[i + 3];
                    if (a == 255)
                    {
                        dst[i] = b;
                        dst[i + 1] = g;
                        dst[i + 2] = r;
                        dst[i + 3] = 255;
                    }
                    else if (a != 0)
                    {
                        dst[i] = (byte)(b * a / 255);
                        dst[i + 1] = (byte)(g * a / 255);
                        dst[i + 2] = (byte)(r * a / 255);
                        dst[i + 3] = a;
                    }
                }
            }
        }
        return writeable;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage([In] SIZE size, [In] uint flags, out IntPtr phbm);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string pszPath, IntPtr pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

    private const uint SIIGBF_ICONONLY = 0x100;
    private const uint SIIGBF_BIGGERSIZEOK = 0x1;
    private const uint SIIGBF_SCALEUP = 0x2;

    private static Bitmap? GetIconFromShellItemImageFactory(string path)
    {
        var hbm = IntPtr.Zero;
        try
        {
            var iid = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");
            if (SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var factory) != 0 || factory == null)
                return null;

            var size = new SIZE { cx = 256, cy = 256 };
            var hr = factory.GetImage(size, SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK | SIIGBF_SCALEUP, out hbm);
            if (hr == unchecked((int)0x8000000A)) // E_PENDING: icon not ready, use the fast SHGetFileInfo fallback
                return null;
            if (hr != 0 || hbm == IntPtr.Zero) return null;

            return HBitmapToBitmap(hbm, path);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hbm != IntPtr.Zero) DeleteObject(hbm);
        }
    }

    private static Bitmap? HBitmapToBitmap(IntPtr hbm, string path)
    {
        var hdc = IntPtr.Zero;
        try
        {
            if (GetObject(hbm, Marshal.SizeOf<BITMAP>(), out var bitmap) == 0) return null;

            var width = Math.Abs(bitmap.bmWidth);
            var height = Math.Abs(bitmap.bmHeight);
            if (width == 0 || height == 0) return null;

            hdc = CreateCompatibleDC(IntPtr.Zero);
            if (hdc == IntPtr.Zero) return null;

            var stride = width * 4;
            var pixels = new byte[stride * height];
            var ptr = Marshal.AllocHGlobal(pixels.Length);
            try
            {
                var bmi = new BITMAPINFO();
                bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
                bmi.bmiHeader.biWidth = width;
                bmi.bmiHeader.biHeight = -height;
                bmi.bmiHeader.biPlanes = 1;
                bmi.bmiHeader.biBitCount = 32;

                if (GetDIBits(hdc, hbm, 0, (uint)height, ptr, ref bmi, 0) == 0) return null;
                Marshal.Copy(ptr, pixels, 0, pixels.Length);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            var writeable = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            using (var framebuffer = writeable.Lock())
            {
                unsafe
                {
                    var dst = new Span<byte>((void*)framebuffer.Address, pixels.Length);
                    for (var i = 0; i < pixels.Length; i += 4)
                    {
                        var b = pixels[i];
                        var g = pixels[i + 1];
                        var r = pixels[i + 2];
                        var a = pixels[i + 3];
                        if (a == 255)
                        {
                            dst[i] = b;
                            dst[i + 1] = g;
                            dst[i + 2] = r;
                            dst[i + 3] = 255;
                        }
                        else if (a != 0)
                        {
                            dst[i] = (byte)(b * a / 255);
                            dst[i + 1] = (byte)(g * a / 255);
                            dst[i + 2] = (byte)(r * a / 255);
                            dst[i + 3] = a;
                        }
                    }
                }
            }
            return writeable;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hdc != IntPtr.Zero) DeleteDC(hdc);
        }
    }

    /// <summary>
    /// Fallback: extract the associated icon via SHGetFileInfo(SHGFI_ICON).
    /// Note: the icon handle is returned in <see cref="SHFILEINFO.hIcon"/>, not as the
    /// function's return value. The shell image list is NOT used because its icons are
    /// colorized (bluish) variants that differ from what Explorer shows.
    /// </summary>
    private static Bitmap? GetIconFromShGetFileInfo(string path)
    {
        const uint SHGFI_ICON = 0x000000100;
        const uint SHGFI_LARGEICON = 0x000000000;
        const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

        var hIcon = IntPtr.Zero;
        try
        {
            var info = new SHFILEINFO();
            var attributes = Directory.Exists(path) ? FILE_ATTRIBUTE_DIRECTORY : 0;
            var flags = SHGFI_ICON | SHGFI_LARGEICON;
            if (!(File.Exists(path) || Directory.Exists(path)))
                flags |= SHGFI_USEFILEATTRIBUTES;

            SHGetFileInfo(path, attributes, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);
            hIcon = info.hIcon;
            if (hIcon == IntPtr.Zero) return null;

            return IconToBitmap(hIcon);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hIcon != IntPtr.Zero) DestroyIcon(hIcon);
        }
    }

    private static Bitmap? IconToBitmap(IntPtr hIcon)
    {
        var hbmColor = IntPtr.Zero;
        var hbmMask = IntPtr.Zero;
        var hdc = IntPtr.Zero;
        try
        {
            if (!GetIconInfo(hIcon, out var iconInfo)) return null;
            hbmColor = iconInfo.hbmColor;
            hbmMask = iconInfo.hbmMask;
            if (hbmColor == IntPtr.Zero) return null;

            if (GetObject(hbmColor, Marshal.SizeOf<BITMAP>(), out var bitmap) == 0) return null;

            var width = Math.Abs(bitmap.bmWidth);
            var height = Math.Abs(bitmap.bmHeight);
            if (width == 0 || height == 0) return null;

            hdc = CreateCompatibleDC(IntPtr.Zero);
            if (hdc == IntPtr.Zero) return null;

            var stride = width * 4;
            var pixels = new byte[stride * height];
            var ptr = Marshal.AllocHGlobal(pixels.Length);
            try
            {
                var bmi = new BITMAPINFO();
                bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
                bmi.bmiHeader.biWidth = width;
                bmi.bmiHeader.biHeight = -height;
                bmi.bmiHeader.biPlanes = 1;
                bmi.bmiHeader.biBitCount = 32;

                if (GetDIBits(hdc, hbmColor, 0, (uint)height, ptr, ref bmi, 0) == 0) return null;
                Marshal.Copy(ptr, pixels, 0, pixels.Length);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            ApplyAlphaFromMask(hdc, hbmMask, width, height, pixels);

            var writeable = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            using (var framebuffer = writeable.Lock())
            {
                unsafe
                {
                    var dst = new Span<byte>((void*)framebuffer.Address, pixels.Length);
                    for (var i = 0; i < pixels.Length; i += 4)
                    {
                        var b = pixels[i];
                        var g = pixels[i + 1];
                        var r = pixels[i + 2];
                        var a = pixels[i + 3];
                        if (a == 255)
                        {
                            dst[i] = b;
                            dst[i + 1] = g;
                            dst[i + 2] = r;
                            dst[i + 3] = 255;
                        }
                        else if (a != 0)
                        {
                            dst[i] = (byte)(b * a / 255);
                            dst[i + 1] = (byte)(g * a / 255);
                            dst[i + 2] = (byte)(r * a / 255);
                            dst[i + 3] = a;
                        }
                    }
                }
            }
            return writeable;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hbmColor != IntPtr.Zero) DeleteObject(hbmColor);
            if (hbmMask != IntPtr.Zero) DeleteObject(hbmMask);
            if (hdc != IntPtr.Zero) DeleteDC(hdc);
        }
    }

    /// <summary>
    /// If the color bitmap has no alpha channel, derive it from the 1bpp AND mask.
    /// </summary>
    private static void ApplyAlphaFromMask(IntPtr hdc, IntPtr hbmMask, int width, int height, byte[] pixels)
    {
        var hasAlpha = false;
        for (var i = 3; i < pixels.Length; i += 4)
        {
            if (pixels[i] != 0) { hasAlpha = true; break; }
        }
        if (hasAlpha || hbmMask == IntPtr.Zero) return;

        if (GetObject(hbmMask, Marshal.SizeOf<BITMAP>(), out var maskBmp) == 0) return;

        var maskStride = ((Math.Abs(maskBmp.bmWidth) + 31) / 32) * 4;
        var maskBytes = new byte[maskStride * Math.Abs(maskBmp.bmHeight)];
        var maskPtr = Marshal.AllocHGlobal(maskBytes.Length);
        try
        {
            var bmi = new BITMAPINFO();
            bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
            bmi.bmiHeader.biWidth = Math.Abs(maskBmp.bmWidth);
            bmi.bmiHeader.biHeight = -Math.Abs(maskBmp.bmHeight);
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 1;

            if (GetDIBits(hdc, hbmMask, 0, (uint)Math.Abs(maskBmp.bmHeight), maskPtr, ref bmi, 0) == 0) return;
            Marshal.Copy(maskPtr, maskBytes, 0, maskBytes.Length);
        }
        finally
        {
            Marshal.FreeHGlobal(maskPtr);
        }

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var bitIndex = y * maskStride + x / 8;
                if (bitIndex >= maskBytes.Length) continue;
                var isMasked = (maskBytes[bitIndex] & (0x80 >> (x % 8))) != 0;
                var offset = y * width * 4 + x * 4;
                pixels[offset + 3] = isMasked ? (byte)0 : (byte)255;
            }
        }
    }
}
