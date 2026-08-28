using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;

namespace uWidgets.Services;

/// <summary>
/// An attached screen: the Avalonia screen geometry, the Windows identity
/// (device name + EDID friendly name) and the matched stored configuration.
/// </summary>
/// <param name="Screen">Avalonia screen (bounds / working area / scaling / primary flag).</param>
/// <param name="Identity">Windows identity used for matching (device name + friendly name + size).</param>
/// <param name="Config">The stored per-screen configuration matched to this screen; <c>null</c> = new screen with no configuration yet.</param>
public sealed record AttachedScreen(Screen Screen, ScreenIdentity Identity, ScreenLayout? Config);

/// <summary>
/// Tracks the attached displays, builds each screen's Windows identity (device
/// name + EDID friendly name + resolution) and matches it to the stored
/// per-screen configurations (<see cref="ScreenMatcher"/>).
/// <para>
/// Display changes (plug / unplug / resolution / DPI / arrangement) are detected
/// by polling the screen signature and raising <see cref="ScreensChanged"/> so the
/// widget factory can hide widgets of removed screens and recreate them when a
/// screen comes back.
/// </para>
/// </summary>
public class DisplayMonitorService(ILayoutProvider layoutProvider)
{
    private Window? anchor;
    private DispatcherTimer? poller;
    private string lastSignature = "";

    /// <summary>Attached screens (geometry + identity + matched configuration), refreshed by <see cref="Refresh"/>. Ordered by screen index.</summary>
    public IReadOnlyList<AttachedScreen> Attached { get; private set; } = [];

    /// <summary>Raised after the attached screen set changed (plug / unplug / resolution / DPI).</summary>
    public event EventHandler? ScreensChanged;

    /// <summary>
    /// Start watching display changes. Call once with any window (a widget or the
    /// settings window); the service keeps a reference to it as the screens anchor
    /// and re-arms itself whenever a window activates.
    /// </summary>
    public void Attach(Window window)
    {
        if (anchor == null)
        {
            anchor = window;
            window.Activated += (_, _) =>
            {
                Refresh();
                EnsurePoller();
            };
            EnsurePoller();
            Refresh();
        }
        else if (!ReferenceEquals(anchor, window))
        {
            // A widget / settings window re-arms the poller (in case the anchor
            // window ever closed); the anchor itself is never replaced.
            EnsurePoller();
        }
    }

    private void EnsurePoller()
    {
        if (poller != null) return;
        poller = new DispatcherTimer(TimeSpan.FromSeconds(1.5), DispatcherPriority.Background, (_, _) => Refresh());
        poller.Start();
    }

    /// <summary>Recalculate the attached screen list and raise <see cref="ScreensChanged"/> on any change.</summary>
    public void Refresh()
    {
        if (anchor == null) return;

        var screens = anchor.Screens.All;
        if (screens.Count == 0) return;

        // Clean redundant empty duplicates first (twin-key drift from stale
        // EnsureConfig); matching then works against a tidy config set.
        var storedLayout = layoutProvider.Get().Deduplicate();
        if (!ReferenceEquals(storedLayout, layoutProvider.Get()))
            layoutProvider.Save(storedLayout);

        // Consuming match: each stored entry matches at most ONE screen, so twin
        // screens with identical keys can never both own the same widgets.
        var consumedConfigIds = new HashSet<string>(StringComparer.Ordinal);

        var devices = EnumerateDevices();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var attached = new List<AttachedScreen>();

        foreach (var screen in screens)
        {
            var device = MatchDevice(screen, devices, used);
            var identity = new ScreenIdentity(
                device?.Name ?? string.Empty,
                device?.FriendlyName ?? "Screen",
                screen.Bounds.Width,
                screen.Bounds.Height,
                screen.Primary,
                screen.Scaling);

            var config = ScreenMatcher.Match(storedLayout.Screens, identity, consumedConfigIds);
            attached.Add(new AttachedScreen(screen, identity, config));
        }

        // Attached is ALWAYS refreshed (queries see the latest configs even when
        // the geometry signature did not change — e.g. right after EnsureConfig
        // created a new entry); ScreensChanged fires only on actual changes.
        Attached = attached;

        var signature = string.Join("|", attached.Select(a =>
            $"{a.Identity.Key}@{a.Screen.Bounds.X},{a.Screen.Bounds.Y}:{a.Screen.Scaling:F2}:{a.Config?.Id ?? "-"}"));
        if (signature == lastSignature) return;

        lastSignature = signature;
        ScreensChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The attached screen the window currently sits on.</summary>
    public AttachedScreen? Find(Window window) =>
        window.Screens.ScreenFromWindow(window) is { } screen ? Find(screen) : null;

    /// <summary>The attached screen matching an Avalonia screen (by bounds).</summary>
    public AttachedScreen? Find(Screen screen) =>
        Attached.FirstOrDefault(a => a.Screen.Bounds == screen.Bounds);

    /// <summary>The attached screen whose stored configuration has the given id.</summary>
    public AttachedScreen? FindByConfigId(string configId) =>
        Attached.FirstOrDefault(a => a.Config?.Id == configId);

    /// <summary>
    /// Ensure a stored configuration exists for an attached screen (new screens
    /// get a fresh entry on their first use) and return it.
    /// </summary>
    public ScreenLayout EnsureConfig(AttachedScreen attached)
    {
        if (attached.Config != null) return attached.Config;

        var entry = new ScreenLayout(
            Guid.NewGuid().ToString("N"),
            attached.Identity.Key,
            null,
            null,
            null,
            null,
            []);
        var screens = layoutProvider.Get().UpsertScreen(entry);
        layoutProvider.Save(screens);

        // Re-run matching so the in-memory list sees the new entry immediately.
        // Without this, a twin screen (same key, another attached display)
        // matching against the stale list would create a SECOND identical entry.
        Refresh();

        var list = Attached.ToList();
        var index = list.FindIndex(a => a.Screen.Bounds == attached.Screen.Bounds);
        if (index >= 0)
        {
            list[index] = attached with { Config = entry };
            Attached = list;
        }

        // First-time screens get their own entry NOW → also split legacy widgets
        // whose positions fall on them (idempotent; keeps the migration complete
        // even when a screen appears for the very first time).
        MigrateLegacyWidgets();

        return entry;
    }

    /// <summary>
    /// Idempotent migration of the legacy "primary" entry: every widget whose
    /// absolute desktop position sits inside ANOTHER attached screen's working
    /// area is moved into that screen's own per-screen entry (coordinates
    /// converted from absolute desktop pixels to relative-to-that-screen).
    /// <para>
    /// v1 layout files store ALL widgets in a single legacy primary entry with
    /// absolute coordinates — including widgets physically placed on a second
    /// screen. While they render correctly, per-screen grids / sizing / editing
    /// then resolve against the wrong screen's grid (the mess reported in the
    /// multi-screen cleanup). After the split, the primary entry owns only what
    /// is really on the primary screen and every screen's grid applies to its
    /// own widgets.
    /// </para>
    /// </summary>
    public void MigrateLegacyWidgets()
    {
        var stored = layoutProvider.Get();
        var primary = stored.FindById(ScreensLayout.LegacyPrimaryId);
        if (primary == null) return;

        var moved = false;
        foreach (var widget in primary.Layout.ToList())
        {
            var centerX = widget.X + widget.Width / 2.0;
            var centerY = widget.Y + widget.Height / 2.0;

            var owner = Attached.FirstOrDefault(a =>
                a.Config != null
                && a.Config.Id != primary.Id
                && a.Screen.WorkingArea.Contains(new PixelPoint((int) centerX, (int) centerY)));

            if (owner == null) continue; // genuinely on the primary (or unmatched yet) → stays

            var area = owner.Screen.WorkingArea;
            var entry = widget with { X = widget.X - area.X, Y = widget.Y - area.Y };

            stored = stored.WithScreen(primary with
            {
                Layout = primary.Layout.Where(w => w != widget).ToList()
            });
            stored = stored.WithScreen(owner.Config with
            {
                Layout = [.. owner.Config.Layout, entry]
            });
            moved = true;
        }

        if (!moved) return;
        layoutProvider.Save(stored);
        Refresh();
    }

    // ---------- Win32 enumeration ----------

    private const int CCHDEVICENAME = 32;
    private const uint DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x1;
    private const uint ENUM_CURRENT_SETTINGS = 0xFFFFFFFF;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICE
    {
        public uint cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)] public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string? lpszDeviceName, uint iModeNum, ref DEVMODE lpDevMode);

    private sealed record Win32Device(string Name, string FriendlyName, int Width, int Height);

    private static List<Win32Device> EnumerateDevices()
    {
        var result = new List<Win32Device>();

        for (uint index = 0; ; index++)
        {
            var device = new DISPLAY_DEVICE { cb = (uint) Marshal.SizeOf<DISPLAY_DEVICE>() };
            if (!EnumDisplayDevices(null, index, ref device, 0)) break;
            if ((device.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) == 0) continue;

            var mode = new DEVMODE { dmSize = (short) Marshal.SizeOf<DEVMODE>() };
            EnumDisplaySettings(device.DeviceName, ENUM_CURRENT_SETTINGS, ref mode);

            result.Add(new Win32Device(
                device.DeviceName,
                string.IsNullOrWhiteSpace(device.DeviceString) ? "Screen" : device.DeviceString,
                mode.dmPelsWidth,
                mode.dmPelsHeight));
        }

        return result;
    }

    private static Win32Device? MatchDevice(Screen screen, List<Win32Device> devices, HashSet<string> used)
    {
        // Prefer an exact resolution match (unique when resolutions differ);
        // fall back to the first unused device in enumeration order.
        var exact = devices.FirstOrDefault(d => !used.Contains(d.Name) && d.Width == screen.Bounds.Width && d.Height == screen.Bounds.Height);
        if (exact != null)
        {
            used.Add(exact.Name);
            return exact;
        }

        var fallback = devices.FirstOrDefault(d => !used.Contains(d.Name));
        if (fallback != null) used.Add(fallback.Name);
        return fallback;
    }
}