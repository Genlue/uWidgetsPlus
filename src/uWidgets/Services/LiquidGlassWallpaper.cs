using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.Win32;
using SkiaSharp;
using uWidgets.Core.Models.Settings;
using uWidgets.Views.Controls;

namespace uWidgets.Services;

/// <summary>
/// Resolves the wallpaper the glass samples. Primary source: a 1:1 capture of the
/// real desktop wallpaper — including animated wallpapers, which do not live in
/// Progman (see <see cref="DesktopCapturer"/>) — so the sampled background is
/// pixel-identical to what is actually behind the widget. The static-file path
/// (Windows wallpaper image + registry placement) remains as a fallback when no
/// capture is available.
/// </summary>
public static class LiquidGlassWallpaper
{
    /// <summary>
    /// Lower bound of the live sampling rate — effectively "as fast as possible".
    /// </summary>
    public const int MinIntervalMs = LiquidGlassSettings.MinLiveSamplingInterval;

    /// <summary>Upper bound of the live sampling rate (1 fps).</summary>
    public const int MaxIntervalMs = LiquidGlassSettings.MaxLiveSamplingInterval;

    /// <summary>Default live sampling interval (10 fps).</summary>
    public const int DefaultIntervalMs = LiquidGlassSettings.DefaultLiveSamplingInterval;

    private static readonly object Gate = new();
    private static WallpaperSnapshot? cached;
    private static string? cachedKey;

    private static bool liveSamplingEnabled = true;
    private static int liveIntervalMs = DefaultIntervalMs;
    private static bool suspended;
    private static DispatcherTimer? liveSamplingTimer;

    /// <summary>
    /// Turn continuous desktop sampling on or off. Switching it off is the static-wallpaper
    /// mode: the last captured frame is frozen (a single capture still runs if nothing has
    /// been sampled yet, so the glass never falls back to a stale wallpaper file).
    /// </summary>
    public static void ConfigureLiveSampling(bool enabled, int intervalMs)
    {
        liveSamplingEnabled = enabled;
        liveIntervalMs = Math.Clamp(intervalMs, MinIntervalMs, MaxIntervalMs);

        if (!OperatingSystem.IsWindows()) return;
        try
        {
            var timer = EnsureTimer();
            timer.Interval = TimeSpan.FromMilliseconds(liveIntervalMs);
            UpdateTimerState();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Live glass sampling unavailable: {ex.Message}");
        }
    }

    /// <summary>
    /// Stop asking for new frames — every screen is covered by a fullscreen application and
    /// nobody can see the glass. Unrelated to the user's 实时采样 switch.
    /// </summary>
    public static void SuspendLiveSampling()
    {
        suspended = true;
        UpdateTimerState();
    }

    /// <summary>Resume asking for frames after a fullscreen application went away.</summary>
    public static void ResumeLiveSampling()
    {
        suspended = false;
        UpdateTimerState();
    }

    /// <summary>True when the user's 实时采样 switch is on.</summary>
    public static bool LiveSamplingEnabled => liveSamplingEnabled;

    /// <summary>True when the sampler is actually ticking (switch on, glass on screen, not covered).</summary>
    public static bool LiveSamplingActive =>
        liveSamplingEnabled && !suspended && liveSamplingTimer is { IsEnabled: true };

    /// <summary>The effective sampling interval in milliseconds.</summary>
    public static int LiveSamplingIntervalMs => liveIntervalMs;

    /// <summary>
    /// Re-evaluate whether the sampler should be ticking. Called when glass surfaces attach or
    /// detach — the sampler is pointless while no widget is on screen, and starting it lazily
    /// also means a theme switch made before any widget existed still begins sampling later.
    /// </summary>
    internal static void RefreshSamplerState() => UpdateTimerState();

    private static DispatcherTimer EnsureTimer()
    {
        if (liveSamplingTimer != null) return liveSamplingTimer;
        liveSamplingTimer = new DispatcherTimer();
        liveSamplingTimer.Tick += OnLiveSamplingTick;
        return liveSamplingTimer;
    }

    private static void UpdateTimerState()
    {
        if (liveSamplingTimer == null) return;
        // DispatcherTimer must be started and stopped from the UI thread.
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(UpdateTimerState);
            return;
        }
        var shouldRun = liveSamplingEnabled && !suspended && LiquidGlassSurface.HasActiveSurfaces;
        if (shouldRun)
        {
            if (!liveSamplingTimer.IsEnabled) liveSamplingTimer.Start();
        }
        else
        {
            liveSamplingTimer.Stop();
        }
    }

    /// <summary>
    /// One sampling tick: drop the cached frame and let the glass surfaces pick up a new one.
    /// <para>
    /// The tick deliberately does <b>not</b> capture on the UI thread — it only invalidates, and the
    /// capture happens on the render worker that calls <see cref="Get"/>.
    /// </para>
    /// <para>
    /// <b>Only ask for a frame that something can consume.</b> A capture plus its shared backdrop
    /// costs ~47 ms on a 2560×1440 desktop, so while every widget is mid-prepare there is nothing to
    /// hand a new frame to. Invalidating anyway used to be actively harmful: it discarded the frame
    /// the busy widgets were rendering from and left a frame "pending", which then held the sampler
    /// back for a fixed retry floor. The net effect was that a <i>smaller</i> interval produced a
    /// <i>slower</i> glass — 3 ms measured 24 publishes/s across six widgets against 60/s at 100 ms,
    /// i.e. 4 fps per widget instead of 10. The interval is therefore a floor on the period between
    /// captures, never a target, and a tick that finds no idle surface simply comes back next time.
    /// </para>
    /// <para>
    /// The staleness guard is the safety net: if nothing has been captured for a long time while
    /// widgets are still asking, the tick forces a frame anyway, so a surface that somehow never
    /// reports itself idle cannot stall live sampling permanently.
    /// </para>
    /// </summary>
    private static void OnLiveSamplingTick(object? sender, EventArgs e)
    {
        if (!liveSamplingEnabled || suspended) { UpdateTimerState(); return; }
        if (!LiquidGlassSurface.HasActiveSurfaces) { UpdateTimerState(); return; }

        var now = Environment.TickCount64;
        // The period is measured from the start of the previous round, not from its capture. A
        // capture lands a few milliseconds after the round begins, so measuring from it made every
        // other tick arrive "too early" and halved the rate — 30 publishes/s instead of 60 at a
        // 100 ms interval.
        if (now - Interlocked.Read(ref lastRoundAt) < liveIntervalMs) return;
        if (LiquidGlassSurface.AnySurfaceRendering && now - Interlocked.Read(ref lastCaptureAt) < StallGuardMs)
            return;

        Invalidate();
        // Immediate: the interval can be shorter than the surface's move/resize debounce, and a
        // debounce restarted on every tick would never elapse — freezing the glass entirely.
        LiquidGlassSurface.RefreshAllImmediate();
    }

    /// <summary>When the desktop was last captured; the stall guard is measured from here.</summary>
    private static long lastCaptureAt;

    /// <summary>When the current sampling round began; the interval is measured from here.</summary>
    private static long lastRoundAt;

    /// <summary>Captures taken since the process started, for the rate diagnostic.</summary>
    private static int captureCount;

    /// <summary>Total desktop captures; the achieved sharing is the publish rate divided by this.</summary>
    internal static int CaptureCount => Volatile.Read(ref captureCount);

    /// <summary>Longest the sampler may go without capturing while widgets are still asking.</summary>
    private const int StallGuardMs = 1000;

    /// <summary>Raised whenever the wallpaper is invalidated (system wallpaper change, display change, or manual refresh).</summary>
    public static event Action? WallpaperInvalidated;

    public static WallpaperSnapshot Get()
    {
        // The cache owns one reference and the caller gets another; the caller releases it with
        // WallpaperSnapshot.Dispose. Nothing here is kept alive by a timer — a capture is a full
        // desktop bitmap and live sampling replaces it ten times a second.
        var snapshot = GetShared();
        snapshot.AddRef();
        return snapshot;
    }

    private static WallpaperSnapshot GetShared()
    {
        lock (Gate)
        {
            if (!OperatingSystem.IsWindows())
                return new WallpaperSnapshot(null, new SKColor(32, 38, 48));
            try
            {
                // Live sampling on: reuse the frame while it is fresh (so every widget samples
                // the same instant), otherwise grab a new one. Live sampling off: the frozen
                // frame wins; a single one-shot capture runs only if there is nothing to freeze.
                if (liveSamplingEnabled && CaptureOnce() is { } live && live.LiveCapture)
                    return live;

                if (!liveSamplingEnabled)
                {
                    if (cached is { LiveCapture: true }) return cached;
                    if (CaptureOnce() is { } once) return once;
                }

                return FromFileFallback();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                return cached ?? new WallpaperSnapshot(null, new SKColor(32, 38, 48));
            }
        }
    }

    /// <summary>Force a fresh capture (e.g. the user pressed 刷新壁纸 or the wallpaper changed).</summary>
    public static void Invalidate()
    {
        WallpaperSnapshot? old;
        lock (Gate)
        {
            old = cached;
            cached = null;
            cachedKey = null;
        }

        Interlocked.Exchange(ref lastRoundAt, Environment.TickCount64);
        old?.Dispose();

        NotifyInvalidated();
    }

    /// <summary>
    /// Drop the cached desktop capture and its decoded bitmap.
    /// <para>
    /// Called while every attached screen is covered by a fullscreen application: the capture is
    /// a full virtual desktop at physical resolution — the single largest allocation in the
    /// process (tens of megabytes with several monitors) — and nothing can be looking at glass
    /// right then. The next <see cref="Get"/> re-captures on demand. Releasing here only drops the
    /// cache's own reference: a render that is still holding one keeps the pixels valid until it
    /// lets go.
    /// </para>
    /// </summary>
    public static void Release()
    {
        WallpaperSnapshot? old;
        lock (Gate)
        {
            old = cached;
            cached = null;
            cachedKey = null;
        }

        old?.Dispose();
    }

    private static void NotifyInvalidated()
    {
        try
        {
            if (Dispatcher.UIThread.CheckAccess()) WallpaperInvalidated?.Invoke();
            else Dispatcher.UIThread.Post(() => WallpaperInvalidated?.Invoke());
        }
        catch
        {
            try { WallpaperInvalidated?.Invoke(); } catch { }
        }
    }

    private static WallpaperSnapshot? CaptureOnce()
    {
        // A captured frame stays valid until something explicitly invalidates it — the live sampler
        // on its next tick, a wallpaper change, a display change or a fullscreen transition. It is
        // deliberately NOT expired on a timer: keying the life of the frame to the sampling interval
        // meant that at a small interval the widgets stopped sharing one capture (and one shared
        // backdrop build) and each paid for its own, which is exactly backwards.
        if (cached is { LiveCapture: true }) return cached;

        // Two attempts: a wallpaper host can vanish mid-transition (monitor hot-plug, a
        // wallpaper engine restarting) and re-resolving usually succeeds on the second try.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var bmp = DesktopCapturer.CaptureBitmap();
            if (bmp == null) continue;

            var snapshot = WallpaperSnapshot.FromBitmap(null, new SKColor(32, 38, 48), bmp, live: true);
            WallpaperSnapshot? old;
            lock (Gate)
            {
                old = cached;
                cached = snapshot;
                cachedKey = null;
            }

            // Frame coherence: every surface asked for a frame from now on samples this one, so a
            // round costs one desktop grab and one backdrop build however many widgets are on screen.
            Interlocked.Exchange(ref lastCaptureAt, Environment.TickCount64);
            Interlocked.Increment(ref captureCount);
            // Only the cache's reference goes; a render still using the previous frame keeps it.
            old?.Dispose();
            return snapshot;
        }
        return null;
    }

    /// <summary>Static file based fallback (Windows wallpaper image + registry placement).</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
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
        if (cachedKey == key && cached is { LiveCapture: false }) return cached;
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
        cached = WallpaperSnapshot.FromBitmap(bytes, background, bmp, style, tile);
        cachedKey = key;
        old?.Dispose();
        return cached;
    }
}

/// <summary>
/// Captures the live desktop wallpaper — exactly what the user sees behind the widgets —
/// including animated wallpapers.
/// <para>
/// A static wallpaper is painted by <c>Progman</c>, so <c>PrintWindow(Progman)</c> is enough.
/// Every wallpaper <i>engine</i> instead reparents its own render window into a <c>WorkerW</c>
/// that sits between the desktop icons and the wallpaper: Wallpaper Engine in its default
/// "desktop" mode does this, and its frames never reach Progman at all. The host is located the
/// documented way — the wallpaper <c>WorkerW</c> is the top-level <c>WorkerW</c> that follows the
/// one owning <c>SHELLDLL_DefView</c> — and the first candidate that yields a plausible
/// wallpaper is remembered, so the source only has to be resolved once.
/// </para>
/// </summary>
public static class DesktopCapturer
{
    private const uint PwRenderFullContent = 0x00000002;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr extraData);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr extraData);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder buffer, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? className, string? windowName);

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

    private static readonly object HostGate = new();
    private static IntPtr resolvedHost;
    private static string resolvedHostDescription = "unresolved";

    /// <summary>Which window the current capture comes from — surfaced for diagnostics.</summary>
    public static string CaptureSourceDescription
    {
        get { lock (HostGate) return resolvedHostDescription; }
    }

    public static SKBitmap? CaptureBitmap()
    {
        var width = GetSystemMetrics(SmCXVirtualScreen);
        var height = GetSystemMetrics(SmCYVirtualScreen);
        if (width <= 0 || height <= 0) return null;

        // Fast path: the host resolved last time. This runs once per sampled frame, so it must not
        // walk every top-level window to rediscover a window that has not changed. The frame is
        // trusted as-is — re-validating every frame would make a genuinely dark scene in an animated
        // wallpaper look like a failed capture and flip the glass back to Progman, which is a
        // visible jump between the animated and the static wallpaper.
        IntPtr remembered;
        lock (HostGate) remembered = resolvedHost;
        if (remembered != IntPtr.Zero && IsWindow(remembered))
        {
            var fast = CaptureWindow(remembered, width, height);
            if (fast != null)
            {
                fast.SetImmutable();
                return fast;
            }
        }

        // Slow path: first frame, or the remembered host died (a wallpaper engine restarting, a
        // monitor hot-plug, a shell restart). Re-resolve from scratch.
        foreach (var (hwnd, description) in ResolveCandidates())
        {
            var bmp = CaptureWindow(hwnd, width, height);
            if (bmp == null) continue;
            if (!LooksLikeWallpaper(bmp))
            {
                bmp.Dispose();
                continue;
            }

            lock (HostGate)
            {
                resolvedHost = hwnd;
                resolvedHostDescription = description;
            }
            bmp.SetImmutable();
            return bmp;
        }
        return null;
    }

    /// <summary>Encode a one-off capture as PNG (diagnostics only).</summary>
    public static byte[] Capture()
    {
        using var bmp = CaptureBitmap();
        if (bmp == null) return [];
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    /// <summary>
    /// Candidate windows in priority order: the animated wallpaper host, then the classic Progman
    /// wallpaper, then any visible child surface of the animated host (some engines render into a
    /// child of the WorkerW rather than into it). The already-remembered host is tried separately,
    /// before this runs, so a steady state never enumerates the window list at all.
    /// </summary>
    private static List<(IntPtr Hwnd, string Description)> ResolveCandidates()
    {
        var result = new List<(IntPtr, string)>();
        var seen = new HashSet<IntPtr>();

        void Add(IntPtr hwnd, string description)
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd) || !seen.Add(hwnd)) return;
            result.Add((hwnd, description));
        }

        var progman = FindWindow("Progman", null);
        var defViewHost = IntPtr.Zero;
        var workerViews = new List<IntPtr>();

        EnumWindows((hwnd, _) =>
        {
            if (ClassNameOf(hwnd) != "WorkerW") return true;
            workerViews.Add(hwnd);
            if (defViewHost == IntPtr.Zero &&
                FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                defViewHost = hwnd;
            return true;
        }, IntPtr.Zero);

        // The wallpaper WorkerW is the top-level WorkerW that follows the icons' WorkerW. When
        // the desktop is static there is no such sibling and this contributes nothing.
        if (defViewHost != IntPtr.Zero)
        {
            var afterIcons = false;
            foreach (var worker in workerViews)
            {
                if (afterIcons) { Add(worker, "WorkerW (animated wallpaper host)"); AddChildren(worker, "WorkerW child"); }
                else if (worker == defViewHost) afterIcons = true;
            }
        }

        // A WorkerW parented to Progman also hosts wallpaper on some shell versions.
        foreach (var worker in workerViews)
            if (IsChildOf(progman, worker)) Add(worker, "WorkerW (Progman child)");

        Add(progman, "Progman");
        return result;

        void AddChildren(IntPtr parent, string label)
        {
            EnumChildWindows(parent, (child, _) =>
            {
                if (IsWindowVisible(child)) Add(child, label);
                return true;
            }, IntPtr.Zero);
        }
    }

    private static bool IsChildOf(IntPtr parent, IntPtr child)
    {
        var current = child;
        for (var depth = 0; depth < 32 && current != IntPtr.Zero; depth++)
        {
            var owner = GetParent(current);
            if (owner == parent) return true;
            if (owner == IntPtr.Zero) return false;
            current = owner;
        }
        return false;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hwnd);

    private static string ClassNameOf(IntPtr hwnd)
    {
        var buffer = new StringBuilder(256);
        GetClassName(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static SKBitmap? CaptureWindow(IntPtr hwnd, int width, int height)
    {
        var screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero) return null;
        var memDc = CreateCompatibleDC(screenDc);
        var bitmap = CreateCompatibleBitmap(screenDc, width, height);
        var previous = SelectObject(memDc, bitmap);
        try
        {
            if (!PrintWindow(hwnd, memDc, PwRenderFullContent)) return null;
            return ReadBitmap(memDc, bitmap, width, height);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Wallpaper capture failed for {ClassNameOf(hwnd)}: {ex.Message}");
            return null;
        }
        finally
        {
            SelectObject(memDc, previous);
            DeleteObject(bitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
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
