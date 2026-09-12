using System.Diagnostics;
using uWidgets.Services;

namespace FullscreenChecks;

/// <summary>
/// Verifies the fullscreen / maximized detection rule that pauses the widgets.
///
/// The decision function (<see cref="FullscreenWatcherService.IsDesktopCovered"/>) is pure,
/// so every case is checked deterministically — including the coordinate pathology observed
/// on the development machine, where a DPI-unaware caller saw a normal window as
/// (480,142)-(2538,1306) on a 2048×1152 screen (DWM physical pixels next to virtualized
/// GetWindowRect values). That must NOT be mistaken for a covered desktop.
///
/// The live foreground-window signals are printed as well, so the Win32 layer is exercised.
/// </summary>
class Program
{
    private static int failures;

    private static readonly ScreenRect Monitor = new(0, 0, 2048, 1152);
    private static readonly ScreenRect WorkArea = new(0, 35, 2048, 1087);

    static int Main()
    {
        Console.WriteLine("=== Fullscreen / maximized detection checks ===");
        Console.WriteLine();

        // --- maximized wins on its own (no coordinates needed) ---
        Covered("a maximized window pauses the widgets", isMaximized: true, window: Normal());
        Covered("a maximized window on a screen it does not fill still pauses",
            isMaximized: true, window: new ScreenRect(378, 114, 2037, 1052));

        // --- geometry: a borderless window that fills the monitor or the work area ---
        Covered("a borderless window filling the monitor pauses", isMaximized: false, window: Monitor);
        Covered("a borderless window filling the work area pauses", isMaximized: false, window: WorkArea);
        Covered("1 px inaccuracy still counts as covering", isMaximized: false, window: new ScreenRect(1, 1, 2047, 1151));

        // --- normal windows keep the widgets alive ---
        Visible("a normal floating window does not pause", window: new ScreenRect(378, 114, 2037, 1052));
        Visible("a half-screen snapped window does not pause", window: new ScreenRect(0, 0, 1024, 1152));
        Visible("a window taller than the work area but not maximized does not pause",
            window: new ScreenRect(200, 0, 1400, 1152));

        // --- the real coordinate pathology (physical DWM bounds, virtualized everything else) ---
        Visible("a window extending past its monitor (DPI mixing) does not pause",
            window: new ScreenRect(480, 142, 2538, 1306));
        Visible("a window starting right of the screen origin does not pause",
            window: new ScreenRect(64, 0, 2048, 1152));

        // --- exclusions ---
        Visible("our own window never pauses", window: Monitor, isOwnProcess: true);
        Visible("the desktop / taskbar never pauses", window: Monitor, isShellWindow: true);
        Visible("a minimized window never pauses", window: Monitor, isMinimized: true);
        Visible("an invisible window never pauses", window: Monitor, isVisible: false);

        // --- multi-screen: a fullscreen window on the secondary screen counts ---
        Covered("a fullscreen window on the second monitor pauses",
            isMaximized: false, window: new ScreenRect(2048, 0, 4096, 1152),
            monitors: [Monitor, new ScreenRect(2048, 0, 4096, 1152)],
            workAreas: [WorkArea, new ScreenRect(2048, 35, 4096, 1087)]);

        // --- live signals (diagnostic) ---
        Console.WriteLine();
        var observed = FullscreenWatcherService.Observe();
        Console.WriteLine($"live foreground window: exists={observed.HasWindow} class='{observed.ClassName}' " +
                          $"pid={observed.ProcessId} own={observed.IsOwnProcess} shell={observed.IsShellWindow} " +
                          $"maximized={observed.IsMaximized} minimized={observed.IsMinimized} visible={observed.IsVisible}");

        var watcher = new FullscreenWatcherService();
        watcher.Evaluate();
        watcher.Evaluate();
        Console.WriteLine($"live watcher verdict: covered={watcher.IsFullscreen} " +
                          "(expected False while a normal window is focused)");
        Check("a non-maximized live foreground window reports 'not covered'", !watcher.IsFullscreen);

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
        return failures == 0 ? 0 : 1;
    }

    private static ScreenRect Normal() => new(378, 114, 2037, 1052);

    private static void Covered(string what, bool isMaximized, ScreenRect window,
        bool isOwnProcess = false, bool isShellWindow = false, bool isMinimized = false, bool isVisible = true,
        IReadOnlyList<ScreenRect>? monitors = null, IReadOnlyList<ScreenRect>? workAreas = null)
    {
        var result = FullscreenWatcherService.IsDesktopCovered(
            isOwnProcess, isShellWindow, isMinimized, isVisible, isMaximized,
            window, monitors ?? [Monitor], workAreas ?? [WorkArea]);
        Check(what, result);
    }

    private static void Visible(string what, ScreenRect window,
        bool isOwnProcess = false, bool isShellWindow = false, bool isMinimized = false, bool isVisible = true,
        bool isMaximized = false)
    {
        var result = FullscreenWatcherService.IsDesktopCovered(
            isOwnProcess, isShellWindow, isMinimized, isVisible, isMaximized,
            window, [Monitor], [WorkArea]);
        Check(what, !result);
    }

    private static void Check(string what, bool ok)
    {
        Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {what}");
        if (!ok) failures++;
    }
}
