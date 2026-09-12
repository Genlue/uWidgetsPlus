using System.Diagnostics;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using uWidgets.Core.Models.Settings;

namespace Folders.Services;

/// <summary>
/// Pre-renders the Liquid Glass background bitmap for BigFolderPopupWindow ahead of time,
/// so when the user clicks to open the expanded folder view, the glass background is
/// immediately available without any latency or delay.
/// </summary>
public static class LiquidGlassPreRenderService
{
    private static readonly object renderLock = new();
    private static CancellationTokenSource? currentCts;
    private static string? lastRenderKey;

    public static Bitmap? CachedPopupBitmap { get; private set; }

    public static event Action? PreRenderCompleted;

    public static void RequestPreRender(
        Point? screenCenter,
        double logicalWidth,
        double logicalHeight,
        double cornerRadius,
        Theme? theme,
        bool isDark,
        Screen? targetScreen,
        IReadOnlyList<Screen>? allScreens)
    {
        if (theme?.IsLiquidGlass != true) return;
        if (!LiquidGlassBridge.IsAvailable) return;

        targetScreen ??= allScreens?.FirstOrDefault();
        if (targetScreen == null) return;

        double scale = targetScreen.Scaling > 0 ? targetScreen.Scaling : 1.0;
        double physWidth = logicalWidth * scale;
        double physHeight = logicalHeight * scale;

        double targetX;
        double targetY;

        if (screenCenter.HasValue)
        {
            targetX = screenCenter.Value.X - physWidth / 2.0;
            targetY = screenCenter.Value.Y - physHeight / 2.0;
        }
        else
        {
            targetX = targetScreen.WorkingArea.X + (targetScreen.WorkingArea.Width - physWidth) / 2.0;
            targetY = targetScreen.WorkingArea.Y + (targetScreen.WorkingArea.Height - physHeight) / 2.0;
        }

        var work = targetScreen.WorkingArea;
        double margin = 16 * scale;
        targetX = Math.Clamp(targetX, work.X + margin, work.X + Math.Max(0, work.Width - physWidth - margin));
        targetY = Math.Clamp(targetY, work.Y + margin, work.Y + Math.Max(0, work.Height - physHeight - margin));

        int renderW = Math.Max(1, (int)Math.Round(physWidth));
        int renderH = Math.Max(1, (int)Math.Round(physHeight));

        var screensList = allScreens ?? [targetScreen];
        var left = screensList.Min(s => s.Bounds.X);
        var top = screensList.Min(s => s.Bounds.Y);
        var desktopWidth = screensList.Max(s => s.Bounds.Right) - left;
        var desktopHeight = screensList.Max(s => s.Bounds.Bottom) - top;

        var key = $"{targetX}_{targetY}_{renderW}_{renderH}_{scale}_{isDark}_{theme.EffectiveLiquidGlass.Blur}_{theme.EffectiveLiquidGlass.Refraction}_{theme.EffectiveLiquidGlass.LightAngle}";
        if (key == lastRenderKey && CachedPopupBitmap != null)
        {
            return;
        }

        lock (renderLock)
        {
            currentCts?.Cancel();
            currentCts = new CancellationTokenSource();
            var token = currentCts.Token;

            Task.Run(() =>
            {
                try
                {
                    if (token.IsCancellationRequested) return;

                    var bytes = LiquidGlassBridge.Render(
                        renderW,
                        renderH,
                        (float)scale,
                        (float)(cornerRadius * scale),
                        (float)(targetX - left),
                        (float)(targetY - top),
                        (float)desktopWidth,
                        (float)desktopHeight,
                        (float)(targetScreen.Bounds.X - left),
                        (float)(targetScreen.Bounds.Y - top),
                        (float)targetScreen.Bounds.Width,
                        (float)targetScreen.Bounds.Height,
                        theme,
                        isDark,
                        settingsSurface: true,
                        pixelScale: 1.0f);

                    if (bytes == null || bytes.Length == 0 || token.IsCancellationRequested) return;

                    Dispatcher.UIThread.Post(() =>
                    {
                        if (token.IsCancellationRequested) return;
                        try
                        {
                            using var stream = new MemoryStream(bytes);
                            var nextBmp = new Bitmap(stream);
                            CachedPopupBitmap?.Dispose();
                            CachedPopupBitmap = nextBmp;
                            lastRenderKey = key;
                            PreRenderCompleted?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to create pre-rendered LiquidGlass bitmap: {ex}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Background LiquidGlass pre-render error: {ex}");
                }
            }, token);
        }
    }
}
