using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using uWidgets.Core.Models.Settings;
using uWidgets.Services;

namespace uWidgets.Views.Controls;

/// <summary>A background-only cached bitmap. Work is coalesced while moving/resizing or editing optics.</summary>
public sealed class LiquidGlassSurface : Control
{
    private static readonly HashSet<LiquidGlassSurface> Active = new();
    private static readonly SemaphoreSlim RenderSlots = new(2);

    public static void RefreshAll()
    {
        foreach (var surface in Active) surface.RequestRender();
    }
    public static readonly StyledProperty<Theme?> MaterialProperty =
        AvaloniaProperty.Register<LiquidGlassSurface, Theme?>(nameof(Material));
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<LiquidGlassSurface, CornerRadius>(nameof(CornerRadius));
    public static readonly StyledProperty<bool> PreviewProperty =
        AvaloniaProperty.Register<LiquidGlassSurface, bool>(nameof(Preview));
    public static readonly StyledProperty<bool> SettingsSurfaceProperty =
        AvaloniaProperty.Register<LiquidGlassSurface, bool>(nameof(SettingsSurface));

    public Theme? Material { get => GetValue(MaterialProperty); set => SetValue(MaterialProperty, value); }
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public bool Preview { get => GetValue(PreviewProperty); set => SetValue(PreviewProperty, value); }
    public bool SettingsSurface { get => GetValue(SettingsSurfaceProperty); set => SetValue(SettingsSurfaceProperty, value); }

    private readonly DispatcherTimer debounce;
    private Window? window;
    private Bitmap? bitmap;
    private bool attached;
    private bool busy;
    private int revision;

    public LiquidGlassSurface()
    {
        IsHitTestVisible = false;
        debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(90) };
        debounce.Tick += (_, _) => { debounce.Stop(); RenderMaterial(); };
        ActualThemeVariantChanged += (_, _) => RequestRender();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        attached = true;
        Active.Add(this);
        window = TopLevel.GetTopLevel(this) as Window;
        if (window != null) window.PositionChanged += OnPositionChanged;
        RequestRender();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        attached = false;
        Active.Remove(this);
        revision++;
        debounce.Stop();
        if (window != null) window.PositionChanged -= OnPositionChanged;
        window = null;
        bitmap?.Dispose();
        bitmap = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e) => RequestRender();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MaterialProperty || change.Property == CornerRadiusProperty
            || change.Property == BoundsProperty || change.Property == IsVisibleProperty
            || change.Property == PreviewProperty || change.Property == SettingsSurfaceProperty)
            RequestRender();
    }

    public void RequestRender()
    {
        revision++;
        if (!attached) return;
        debounce.Stop();
        if (!IsVisible || Material?.IsLiquidGlass != true)
        {
            bitmap?.Dispose();
            bitmap = null;
            InvalidateVisual();
            return;
        }
        debounce.Start();
    }

    private async void RenderMaterial()
    {
        if (busy || !attached || !IsVisible || Material?.IsLiquidGlass != true || Bounds.Width < 1 || Bounds.Height < 1) return;
        busy = true;
        var current = revision;
        try
        {
            var scaling = window?.RenderScaling ?? 1;
            // Bound CPU/memory use for very large widgets. Text is rendered separately at native DPI.
            var quality = Math.Min(1, Math.Min(2048 / (Math.Max(Bounds.Width, Bounds.Height) * scaling),
                Math.Sqrt(1200000 / (Bounds.Width * Bounds.Height * scaling * scaling))));
            var renderScale = (float)(scaling * quality);
            var width = Math.Max(1, (int)Math.Ceiling(Bounds.Width * renderScale));
            var height = Math.Max(1, (int)Math.Ceiling(Bounds.Height * renderScale));
            var screen = window?.Screens.ScreenFromWindow(window);
            var screens = window?.Screens.All;
            var left = screens?.Min(s => s.Bounds.X) ?? 0;
            var top = screens?.Min(s => s.Bounds.Y) ?? 0;
            var desktopWidth = (screens?.Max(s => s.Bounds.Right) ?? 1920) - left;
            var desktopHeight = (screens?.Max(s => s.Bounds.Bottom) ?? 1080) - top;
            var position = window != null ? this.PointToScreen(default) : default;
            var frame = new LiquidGlassRenderer.Frame(width, height, renderScale, (float)CornerRadius.TopLeft,
                (float)((position.X - left) * quality), (float)((position.Y - top) * quality),
                (float)(desktopWidth * quality), (float)(desktopHeight * quality),
                (float)(((screen?.Bounds.X ?? 0) - left) * quality), (float)(((screen?.Bounds.Y ?? 0) - top) * quality),
                (float)((screen?.Bounds.Width ?? 1920) * quality), (float)((screen?.Bounds.Height ?? 1080) * quality),
                Material, ActualThemeVariant == ThemeVariant.Dark, SettingsSurface, (float)quality);
            if (Preview)
                frame = frame with { DesktopX = 70 * renderScale, DesktopY = 40 * renderScale, ScreenX = 0, ScreenY = 0,
                    DesktopWidth = 120 * renderScale, DesktopHeight = 90 * renderScale,
                    ScreenWidth = 120 * renderScale, ScreenHeight = 90 * renderScale,
                    Theme = Material with { LiquidGlass = Material.EffectiveLiquidGlass with
                    { Blur = Material.EffectiveLiquidGlass.Blur * 0.35, EdgeWidth = Math.Max(4, Material.EffectiveLiquidGlass.EdgeWidth * 0.35) } } };
            byte[] bytes;
            await RenderSlots.WaitAsync();
            try
            {
                if (!attached || revision != current) return;
                var preview = Preview;
                bytes = await Task.Run(() =>
                {
                    var wallpaper = LiquidGlassWallpaper.Get();
                    return LiquidGlassRenderer.Render(frame, preview ? wallpaper with { Style = "10", Tile = false } : wallpaper);
                });
            }
            finally { RenderSlots.Release(); }
            if (!attached || revision != current) return;
            using var stream = new MemoryStream(bytes);
            var next = new Bitmap(stream);
            bitmap?.Dispose();
            bitmap = next;
            InvalidateVisual();
        }
        catch (Exception ex)
        {
            // A missing/unsupported wallpaper or rendering device must not crash a widget.
            Debug.WriteLine($"Liquid glass render failed: {ex}");
            InvalidateVisual();
        }
        finally
        {
            busy = false;
            if (attached && revision != current) RequestRender();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Material?.IsLiquidGlass != true) return;
        var rect = new Rect(Bounds.Size);
        if (bitmap != null) context.DrawImage(bitmap, rect);
        else
        {
            // Readable first frame/fallback until the background bitmap is ready.
            var dark = ActualThemeVariant == ThemeVariant.Dark;
            var hex = dark ? Material.EffectiveSolidBackgroundDark : Material.EffectiveSolidBackgroundLight;
            var color = Color.TryParse(hex, out var parsed) ? parsed : dark ? Colors.Black : Colors.White;
            context.DrawRectangle(new SolidColorBrush(color, 0.88), new Pen(new SolidColorBrush(Colors.White, 0.45), 1),
                rect.Deflate(0.5), CornerRadius.TopLeft, CornerRadius.TopLeft);
        }
    }
}
