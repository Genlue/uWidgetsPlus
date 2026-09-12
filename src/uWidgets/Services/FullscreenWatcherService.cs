using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace uWidgets.Services;

/// <summary>A screen or window rectangle in physical pixels.</summary>
public readonly record struct ScreenRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

/// <summary>
/// Watches for a foreground application that covers the desktop — fullscreen games, video
/// players, borderless-fullscreen apps, and simply a <b>maximized</b> window — so the host
/// can hide the widgets, pause their timers and release their material caches while the user
/// cannot see them anyway.
/// <para>
/// Detection combines three signals, because none of them is sufficient alone:
/// <list type="bullet">
/// <item><see cref="SHQueryUserNotificationState"/> reports <c>QUNS_RUNNING_D3D_FULL_SCREEN</c>
/// / <c>QUNS_PRESENTATION_MODE</c> for exclusive-fullscreen and presentation modes, but many
/// borderless-fullscreen games report the ordinary "accepts notifications" state.</item>
/// <item><see cref="IsZoomed"/> marks a maximized window. Desktop widgets live at the bottom
/// of the z-order, so a maximized window hides every one of them just as effectively as a
/// fullscreen one — this is the signal the user asked for, and it needs no coordinates at all,
/// which makes it immune to DPI coordinate mixing.</item>
/// <item>Geometry, for borderless windows that fill the screen without the <c>WS_MAXIMIZE</c>
/// style: the window rectangle must cover at least 98% of a screen's full rectangle
/// (true fullscreen) or of its work area (custom chrome maximized).</item>
/// </list>
/// </para>
/// <para>
/// Both sides of every geometric comparison must live in the SAME coordinate space: DWM
/// reports physical pixels, while <c>GetWindowRect</c> / <c>GetMonitorInfo</c> report
/// DPI-virtualized coordinates for a DPI-unaware caller. Mixing them made a normal window
/// look like it extended past its own monitor ((480,142)-(2538,1306) on a 2048×1152 screen).
/// Physical window bounds are therefore compared against Avalonia's physical screen
/// rectangles, and the virtualized window rectangle against the Win32 monitor rectangle.
/// </para>
/// <para>
/// Our own windows, the shell (desktop / taskbar) and the "no foreground window" case are
/// excluded, and entering the covered state requires two consecutive positive polls so a
/// transient window cannot hide the widgets by accident. Leaving takes effect immediately.
/// </para>
/// </summary>
public class FullscreenWatcherService : IDisposable
{
    /// <summary>Poll interval; the check is a few Win32 calls, far cheaper than a frame.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(1200);

    /// <summary>Consecutive positive polls required before reporting the covered state.</summary>
    private const int EnterThreshold = 2;

    /// <summary>Fraction of a screen a borderless window must cover to count as maximized.</summary>
    private const double CoverageThreshold = 0.98;

    private DispatcherTimer? poller;
    private int consecutiveHits;
    private Window? anchor;

    /// <summary>
    /// Raised with <c>true</c> when a fullscreen or maximized application takes over the
    /// desktop, <c>false</c> when the widgets are visible again.
    /// </summary>
    public event EventHandler<bool>? FullscreenChanged;

    /// <summary>Whether the desktop is currently covered (fullscreen or maximized application).</summary>
    public bool IsFullscreen { get; private set; }

    /// <summary>Start watching. Any window can serve as the dispatcher anchor.</summary>
    public void Attach(Window anchor)
    {
        this.anchor ??= anchor;
        if (poller != null) return;
        poller = new DispatcherTimer(PollInterval, DispatcherPriority.Background, (_, _) => Evaluate());
        poller.Start();
    }

    /// <summary>Run one detection pass and raise <see cref="FullscreenChanged"/> on a change.</summary>
    public void Evaluate()
    {
        if (!OperatingSystem.IsWindows()) return;
        Report(ObserveDesktop());
    }

    /// <summary>Feed a detection result through the debounce and raise the change event.</summary>
    private void Report(bool covered)
    {
        if (covered)
        {
            // Debounce: a window that only appears for a moment (splash screens,
            // alt-tab previews) must not hide the widgets.
            if (!IsFullscreen && ++consecutiveHits < EnterThreshold) return;
        }
        else
        {
            consecutiveHits = 0;
            if (!IsFullscreen) return;
        }

        if (covered == IsFullscreen) return;

        IsFullscreen = covered;
        try
        {
            FullscreenChanged?.Invoke(this, covered);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FullscreenWatcher] handler failed: {ex.Message}");
        }
    }

    // ---------- Observation (Win32) ----------

    private bool ObserveDesktop()
    {
        try
        {
            // Exclusive fullscreen / presentation mode are unambiguous.
            if (SHQueryUserNotificationState(out var state) == 0 &&
                (state == QUNS_RUNNING_D3D_FULL_SCREEN || state == QUNS_PRESENTATION_MODE))
            {
                return true;
            }

            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;

            GetWindowThreadProcessId(hwnd, out var pid);
            var isOwnProcess = pid == Environment.ProcessId;
            var isShellWindow = IsShellWindow(hwnd);
            var isMinimized = IsIconic(hwnd);
            var isVisible = IsWindowVisible(hwnd);
            var isMaximized = IsZoomed(hwnd);

            if (isOwnProcess || isShellWindow || isMinimized || !isVisible) return false;

            // Physical window bounds (DWM) against Avalonia's physical screen rectangles.
            if (TryGetExtendedFrameBounds(hwnd, out var physical) && anchor?.Screens.All is { Count: > 0 } screens)
            {
                var bounds = new List<ScreenRect>();
                var workAreas = new List<ScreenRect>();
                foreach (var screen in screens)
                {
                    bounds.Add(ToRect(screen.Bounds));
                    workAreas.Add(ToRect(screen.WorkingArea));
                }

                if (IsDesktopCovered(false, false, false, true, isMaximized, ToRect(physical), bounds, workAreas))
                    return true;
            }

            // Virtualized window bounds (GetWindowRect) against the monitor rectangle, which
            // the same DPI-unaware caller sees virtualized as well.
            if (!GetWindowRect(hwnd, out var virtualized)) return false;

            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero) return false;

            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(monitor, ref info)) return false;

            return IsDesktopCovered(false, false, false, true, isMaximized, ToRect(virtualized),
                [ToRect(info.rcMonitor)], [ToRect(info.rcWork)]);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FullscreenWatcher] detection failed: {ex.Message}");
            return false;
        }
    }

    private static bool IsShellWindow(IntPtr hwnd) => GetClassNameString(hwnd) is
        "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Windows.UI.Core.CoreWindow";

    // ---------- Decision (pure, testable) ----------

    /// <summary>
    /// Pure decision function: is the desktop covered by the foreground window?
    /// Kept free of Win32 so the whole rule set can be verified without a live desktop
    /// (see <c>tests/FullscreenChecks</c>).
    /// </summary>
    /// <param name="isOwnProcess">The foreground window belongs to uWidgets itself.</param>
    /// <param name="isShellWindow">The foreground window is the desktop / wallpaper host / taskbar.</param>
    /// <param name="isMinimized">The window is minimized.</param>
    /// <param name="isVisible">The window is visible.</param>
    /// <param name="isMaximized">The window carries the maximized state.</param>
    /// <param name="window">Window rectangle, in the same coordinate space as the screens.</param>
    /// <param name="monitorBounds">Full screen rectangles (taskbar included).</param>
    /// <param name="workAreas">Screen work areas (taskbar excluded).</param>
    public static bool IsDesktopCovered(
        bool isOwnProcess,
        bool isShellWindow,
        bool isMinimized,
        bool isVisible,
        bool isMaximized,
        ScreenRect window,
        IReadOnlyList<ScreenRect> monitorBounds,
        IReadOnlyList<ScreenRect> workAreas)
    {
        if (isOwnProcess || isShellWindow || isMinimized || !isVisible) return false;

        // A maximized window hides every desktop widget.
        if (isMaximized) return true;

        foreach (var screen in monitorBounds)
            if (Covers(window, screen)) return true;

        foreach (var area in workAreas)
            if (Covers(window, area)) return true;

        return false;
    }

    /// <summary>Does the window rectangle cover at least 98% of the screen rectangle (both axes)?</summary>
    private static bool Covers(ScreenRect window, ScreenRect screen)
    {
        if (screen.Width <= 0 || screen.Height <= 0) return false;

        var overlapWidth = Math.Min(window.Right, screen.Right) - Math.Max(window.Left, screen.Left);
        var overlapHeight = Math.Min(window.Bottom, screen.Bottom) - Math.Max(window.Top, screen.Top);
        if (overlapWidth <= 0 || overlapHeight <= 0) return false;

        return overlapWidth >= screen.Width * CoverageThreshold &&
               overlapHeight >= screen.Height * CoverageThreshold;
    }

    // ---------- Win32 ----------

    private const uint QUNS_RUNNING_D3D_FULL_SCREEN = 3;
    private const uint QUNS_PRESENTATION_MODE = 4;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const uint DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct NATIVE_RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public NATIVE_RECT rcMonitor;
        public NATIVE_RECT rcWork;
        public uint dwFlags;
    }

    private static ScreenRect ToRect(NATIVE_RECT rect) => new(rect.Left, rect.Top, rect.Right, rect.Bottom);

    private static ScreenRect ToRect(PixelRect rect) => new(rect.X, rect.Y, rect.Right, rect.Bottom);

    /// <summary>
    /// Real visual bounds of a window in physical pixels: the DWM extended frame excludes
    /// the invisible resize border that <see cref="GetWindowRect"/> includes.
    /// </summary>
    private static bool TryGetExtendedFrameBounds(IntPtr hwnd, out NATIVE_RECT bounds) =>
        DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out bounds, Marshal.SizeOf<NATIVE_RECT>()) == 0;

    private static string GetClassNameString(IntPtr hwnd)
    {
        var buffer = new StringBuilder(256);
        return GetClassName(hwnd, buffer, buffer.Capacity) > 0 ? buffer.ToString() : string.Empty;
    }

    public void Dispose()
    {
        poller?.Stop();
        poller = null;
        GC.SuppressFinalize(this);
    }

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out uint state);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out NATIVE_RECT rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder className, int maxCount);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, uint attribute, out NATIVE_RECT value, int size);

    /// <summary>Exposes the raw foreground-window signals for the detection probe.</summary>
    public static ForegroundObservation Observe()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return new ForegroundObservation();

        GetWindowThreadProcessId(hwnd, out var pid);
        return new ForegroundObservation
        {
            HasWindow = true,
            ProcessId = pid,
            ClassName = GetClassNameString(hwnd),
            IsOwnProcess = pid == Environment.ProcessId,
            IsShellWindow = IsShellWindow(hwnd),
            IsMaximized = IsZoomed(hwnd),
            IsMinimized = IsIconic(hwnd),
            IsVisible = IsWindowVisible(hwnd)
        };
    }

    /// <summary>Raw foreground-window signals, for diagnostics.</summary>
    public readonly record struct ForegroundObservation
    {
        public bool HasWindow { get; init; }
        public int ProcessId { get; init; }
        public string ClassName { get; init; }
        public bool IsOwnProcess { get; init; }
        public bool IsShellWindow { get; init; }
        public bool IsMaximized { get; init; }
        public bool IsMinimized { get; init; }
        public bool IsVisible { get; init; }
    }
}
