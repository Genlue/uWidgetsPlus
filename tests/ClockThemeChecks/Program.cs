using System.Reflection;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Clock.Models;
using Clock.Services;
using Clock.Views;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;
using uWidgets.Core.Models.Settings;
using uWidgets.Services;

namespace ClockThemeChecks;

/// <summary>
/// Regression checks for the frameless clock's per-widget theme override and its glyph
/// liquid glass output.
///
/// Part 1 (activation): the desktop widget is activated by
/// <c>WidgetFactory.CreateWidgetControl</c> as <c>Activate(typeof(FramelessDigital), layoutProvider, model)</c>,
/// which goes through <c>ActivatorUtilities.CreateInstance</c>. That type has five public
/// constructors, two of which accept those two arguments, so which one wins decides whether
/// the widget ever receives <see cref="IAppSettingsProvider"/> — and therefore whether
/// "跟随软件主题" can resolve the global material at all.
///
/// Part 2 (rendering): renders the control for every theme mode with a liquid glass global
/// theme and saves PNGs, so the follow-global result can be compared with the explicit
/// liquid glass result pixel by pixel (and inspected by eye).
/// </summary>
class Program
{
    private static int failures;
    private static string outputDir = "dist/clock-theme-checks";

    /// <summary>Global material used by the checks ("跟随软件主题" resolves against this).</summary>
    private static readonly SurfaceStyle GlobalSurface = SurfaceStyle.LiquidGlass;

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0) outputDir = args[0];
        Directory.CreateDirectory(outputDir);

        AppBuilder.Configure<Application>().UsePlatformDetect().SetupWithoutStarting();
        Application.Current!.Styles.Add(new FluentTheme());

        var settings = new StubSettings(BuildSettings(GlobalSurface));
        var services = new ServiceCollection();
        services.AddSingleton<IAppSettingsProvider>(settings);
        var provider = services.BuildServiceProvider();
        var layout = new StubLayout();

        Console.WriteLine("=== Frameless clock theme checks ===");
        Console.WriteLine($"global surface = {GlobalSurface}");
        Console.WriteLine();

        // ---- Part 1: activation / theme resolution ----
        var followGlobal = (FramelessDigital)ActivatorUtilities.CreateInstance(
            provider, typeof(FramelessDigital), layout, new FramelessClockModel(ThemeMode: 0));

        var injected = ReadField(followGlobal, "appSettingsProvider");
        Console.WriteLine("ctor args = [IWidgetLayoutProvider, FramelessClockModel]");
        Console.WriteLine($"  injected IAppSettingsProvider = {(injected == null ? "NULL" : injected.GetType().Name)}");
        Check("follow-global receives IAppSettingsProvider", injected != null);
        Check("ThemeMode=0 + global LiquidGlass resolves LiquidGlass",
            ResolveTheme(followGlobal) == (false, true, false));

        var acrylic = (FramelessDigital)ActivatorUtilities.CreateInstance(
            provider, typeof(FramelessDigital), layout, new FramelessClockModel(ThemeMode: 1));
        Check("ThemeMode=1 resolves Acrylic only", ResolveTheme(acrylic) == (true, false, false));

        var solid = (FramelessDigital)ActivatorUtilities.CreateInstance(
            provider, typeof(FramelessDigital), layout, new FramelessClockModel(ThemeMode: 3));
        Check("ThemeMode=3 resolves Solid only", ResolveTheme(solid) == (false, false, true));

        // ---- Part 2: rendering ----
        Console.WriteLine();
        Console.WriteLine($"Rendering into {Path.GetFullPath(outputDir)} …");

        var mode0 = Render(settings, layout, outputDir, "mode0-follow-global", ThemeMode: 0, rendered: true);
        var mode2 = Render(settings, layout, outputDir, "mode2-liquidglass", ThemeMode: 2, rendered: true);
        Render(settings, layout, outputDir, "mode1-acrylic", ThemeMode: 1, rendered: false);

        if (mode0 != null && mode2 != null)
        {
            var diff = MeanAbsoluteDifference(mode0, mode2);
            Console.WriteLine($"  mean |delta| follow-global vs explicit liquid glass: {diff:F2}/255");
            Check("follow-global renders liquid glass, not acrylic (< 12/255 from explicit)", diff < 12.0);
        }
        else
        {
            Check("both liquid glass renders produced pixels", false);
        }

        // ---- Part 3: glyph optics — the meniscus must exist at the default 0 ----
        // Regression: refractionWidth 0 used to mean "no lens", so the numerals were only a
        // blurred, tinted fill and read as 毛玻璃 while the rest of the desktop was 液态玻璃.
        Console.WriteLine();
        Console.WriteLine("--- glyph lens optics ---");

        var globalOptics = new LiquidGlassSettings(
            Blur: 30, Refraction: 60, EdgeWidth: 7, Highlight: 50, Dispersion: 100, LightAngle: 225, EdgeTint: 10);
        const float probeScale = 2f, probeStrokeRadius = 26f;

        var auto = GlyphLiquidGlassRenderer.ResolveLens(globalOptics, probeScale, probeStrokeRadius, 1f, 1f, 0.0);
        Console.WriteLine($"  refractionWidth = 0 (default) → lens {auto.LensWidth:F2}px, bend {auto.LensShift:F2}px, dispersion {auto.Dispersion:F2}");
        Check("default refractionWidth=0 still produces a real lens", auto.LensWidth > 2f && auto.LensShift > 0.5f);

        var fromNull = GlyphLiquidGlassRenderer.ResolveLens(globalOptics, probeScale, probeStrokeRadius, 1f, 1f, null);
        Check("null refractionWidth resolves the same adaptive lens as 0",
            Math.Abs(fromNull.LensWidth - auto.LensWidth) < 0.001f);

        var manual = GlyphLiquidGlassRenderer.ResolveLens(globalOptics, probeScale, probeStrokeRadius, 1f, 1f, 12.0);
        Console.WriteLine($"  refractionWidth = 12 (manual)  → lens {manual.LensWidth:F2}px, bend {manual.LensShift:F2}px");
        Check("an explicit refractionWidth still overrides the adaptive lens", manual.LensWidth > auto.LensWidth + 1f);

        var strong = GlyphLiquidGlassRenderer.ResolveLens(globalOptics with { Refraction = 100 }, probeScale, probeStrokeRadius, 1f, 1f, 0.0);
        Check("the global refraction slider drives the glyph bend", strong.LensShift > auto.LensShift);

        // A mask with no interior pixels used to invert a Math.Clamp range and throw, which
        // silently killed the background render (the widget then kept its flat fallback wash).
        var degenerate = GlyphLiquidGlassRenderer.ResolveLens(globalOptics, probeScale, 2.0f * probeScale, 1f, 1f, 0.0);
        Check("a hairline glyph mask cannot abort the render", degenerate.LensWidth >= 0f);

        // ---- Part 4: the live desktop capture must actually be sampled ----
        // A live capture carries only CachedBitmap (ImageBytes is null). Reading ImageBytes
        // instead — as the 1.8.0 build did — dropped the capture entirely and painted the
        // numerals over a flat colour, losing both the wallpaper and the refraction.
        Console.WriteLine();
        Console.WriteLine("--- live desktop capture sampling ---");

        var glassTheme = new Theme(
            DarkMode: true, AccentColor: null, OpacityLevel: 0.18, Monochrome: false, UseNativeFrame: false,
            FontFamily: "Segoe UI", Surface: SurfaceStyle.LiquidGlass, LiquidGlass: globalOptics);

        using var syntheticWallpaper = new SKBitmap(new SKImageInfo(64, 64, SKColorType.Bgra8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(syntheticWallpaper))
        {
            canvas.Clear(new SKColor(220, 40, 40));
            using var blue = new SKPaint { Color = new SKColor(40, 80, 220) };
            canvas.DrawRect(new SKRect(32, 0, 64, 64), blue);
        }
        syntheticWallpaper.SetImmutable();

        var liveWallpaper = new WallpaperSnapshot(null, new SKColor(0, 0, 0), LiveCapture: true, CachedBitmap: syntheticWallpaper);
        var liveFrame = new LiquidGlassRenderer.Frame(
            64, 64, 1f, 0f, 0, 0, 64, 64, 0, 0, 64, 64, glassTheme, Dark: true);

        // A filled numeral bar with real interior depth (a glyph-like mask).
        var barMask = new byte[64 * 64];
        for (var y = 12; y < 52; y++)
        for (var x = 12; x < 52; x++)
            barMask[y * 64 + x] = 255;

        var livePng = GlyphLiquidGlassRenderer.Render(liveFrame, liveWallpaper, barMask, 0.0);
        var livePixels = DecodePixels(livePng);
        var (red, blue2) = CountDominant(livePixels);
        Console.WriteLine($"  numeral pixels: {red} wallpaper-red, {blue2} wallpaper-blue");
        Check("live capture reaches the numerals (wallpaper colours survive)",
            livePixels != null && red > 100 && blue2 > 100);
        liveWallpaper.Dispose();

        // ---- Part 5: visual preview against a synthetic wallpaper ----
        // The harness has no real desktop capture, so the lens is previewed against generated
        // vertical stripes: the displacement shows up as bent, locally compressed lines near
        // the stroke edges (a plain frosted fill leaves them uniformly blurred instead).
        Console.WriteLine();
        Console.WriteLine("--- optical preview ---");

        const int pw = 736, ph = 368;
        using var stripes = new SKBitmap(new SKImageInfo(pw, ph, SKColorType.Bgra8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(stripes))
        {
            canvas.Clear(new SKColor(16, 22, 36));
            // Wide stripes (32 px period) so the displacement stays readable after the blur.
            for (var i = 0; i < 24; i++)
            {
                using var paint = new SKPaint { Color = SKColor.FromHsl(i * 15f, 78f, 55f) };
                canvas.DrawRect(new SKRect(i * 32, 0, i * 32 + 18, ph), paint);
            }
        }
        stripes.SetImmutable();

        var previewWallpaper = new WallpaperSnapshot(null, new SKColor(16, 22, 36), LiveCapture: true, CachedBitmap: stripes);
        var previewFrame = new LiquidGlassRenderer.Frame(
            pw, ph, 2f, 0f, 0, 0, pw, ph, 0, 0, pw, ph, glassTheme, Dark: true, Columns: 4, Rows: 2);

        var previewMask = new byte[pw * ph];
        for (var y = 60; y < ph - 60; y++)
        for (var x = 90; x < pw - 90; x++)
            previewMask[y * pw + x] = 255;

        var lensPng = GlyphLiquidGlassRenderer.Render(previewFrame, previewWallpaper, previewMask, 0.0);
        var flatPng = GlyphLiquidGlassRenderer.Render(previewFrame, previewWallpaper, previewMask, 0.1);
        var lensPixels = DecodePixels(lensPng);
        var flatPixels = DecodePixels(flatPng);
        var lensDelta = lensPixels != null && flatPixels != null
            ? MeanAbsoluteDifference(lensPixels, flatPixels)
            : 0.0;
        Console.WriteLine($"  mean |delta| lens (auto) vs hairline lens: {lensDelta:F2}/255");
        Check("the default lens visibly bends the backdrop", lensDelta > 2.0);

        var previewPath = Path.Combine(outputDir, "glyph-lens-preview.png");
        File.WriteAllBytes(previewPath, lensPng);
        Console.WriteLine($"  lensed numerals: saved {previewPath}");
        previewWallpaper.Dispose();

        // ---- Part 6: live theme switch on one running instance ----
        // The user changes 外观 → 应用主题 while the clock is already on the desktop: the same
        // widget instance has to follow the new global material without being recreated. This
        // pins that path (settings change → resolve → re-render), which a fresh start does not
        // exercise.
        Console.WriteLine();
        Console.WriteLine("--- live theme switch (acrylic → liquid glass) ---");

        var switchSettings = new StubSettings(BuildSettings(SurfaceStyle.Acrylic));
        var switchServices = new ServiceCollection();
        switchServices.AddSingleton<IAppSettingsProvider>(switchSettings);
        var switchProvider = switchServices.BuildServiceProvider();

        var switched = (FramelessDigital)ActivatorUtilities.CreateInstance(
            switchProvider, typeof(FramelessDigital), layout, new FramelessClockModel(ThemeMode: 0));
        switched.Width = 368;
        switched.Height = 184;
        switched.Measure(new Size(368, 184));
        switched.Arrange(new Rect(0, 0, 368, 184));
        switched.UpdateLayout();

        Console.WriteLine($"  before: {Describe(ResolveTheme(switched))}");
        Check("a follow-global clock starts on the acrylic global theme", ResolveTheme(switched) == (true, false, false));

        switchSettings.Save(BuildSettings(SurfaceStyle.LiquidGlass));

        Console.WriteLine($"  after : {Describe(ResolveTheme(switched))}");
        Check("the same instance follows the switch to liquid glass", ResolveTheme(switched) == (false, true, false));
        Check("and it renders a liquid glass frame afterwards", WaitForFrame(switched));

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
        return failures == 0 ? 0 : 1;
    }

    private static string Describe((bool IsAcrylic, bool IsLiquidGlass, bool IsSolid) theme) =>
        theme switch
        {
            (true, _, _) => "acrylic",
            (_, true, _) => "liquid glass",
            _ => "solid"
        };

    /// <summary>Pump the dispatcher until the widget produced a liquid glass frame.</summary>
    private static bool WaitForFrame(FramelessDigital view)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline && ReadField(view, "liquidGlassBitmap") == null)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(50);
        }

        Dispatcher.UIThread.RunJobs();
        return ReadField(view, "liquidGlassBitmap") != null;
    }

    /// <summary>
    /// Render the widget off-screen at the real 4×2 grid size. The liquid glass material is
    /// produced by a background task, so the dispatcher is pumped until the widget's
    /// pre-rendered bitmap lands (or the timeout elapses). Returns the rendered pixels.
    /// </summary>
    private static byte[]? Render(IAppSettingsProvider provider, StubLayout layout, string dir,
        string name, int ThemeMode, bool rendered)
    {
        const double width = 368, height = 184;

        var model = new FramelessClockModel(
            Use24Hours: true, FontFamily: "Impact", FontWeight: 800, StretchFill: true, ThemeMode: ThemeMode);

        var view = new FramelessDigital(model, layout, provider) { Width = width, Height = height };
        view.Measure(new Size(width, height));
        view.Arrange(new Rect(0, 0, width, height));
        view.UpdateLayout();

        if (rendered)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline && ReadField(view, "liquidGlassBitmap") == null)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(50);
            }
            Dispatcher.UIThread.RunJobs();
            Console.WriteLine($"  {name}: liquidGlassBitmap={(ReadField(view, "liquidGlassBitmap") != null ? "ready" : "NOT PRODUCED")}");
        }

        const double scale = 2.0;
        var pixelSize = new PixelSize((int)(width * scale), (int)(height * scale));
        using var bitmap = new RenderTargetBitmap(pixelSize, new Vector(96 * scale, 96 * scale));
        bitmap.Render(view);

        var path = Path.Combine(dir, $"{name}.png");
        bitmap.Save(path);
        Console.WriteLine($"  {name}: saved {path}");

        return LoadPixels(path);
    }

    /// <summary>Decode a PNG into BGRA bytes (SkiaSharp, so no unsafe pointer juggling).</summary>
    private static byte[]? LoadPixels(string path)
    {
        using var decoded = SKBitmap.Decode(path);
        if (decoded == null) return null;

        var pixels = new byte[decoded.ByteCount];
        System.Runtime.InteropServices.Marshal.Copy(decoded.GetPixels(), pixels, 0, pixels.Length);
        return pixels;
    }

    /// <summary>Decode a PNG byte buffer into BGRA bytes.</summary>
    private static byte[]? DecodePixels(byte[] png)
    {
        using var decoded = SKBitmap.Decode(png);
        if (decoded == null) return null;
        var pixels = new byte[decoded.ByteCount];
        System.Runtime.InteropServices.Marshal.Copy(decoded.GetPixels(), pixels, 0, pixels.Length);
        return pixels;
    }

    /// <summary>Count pixels whose red / blue channel dominates (BGRA order).</summary>
    private static (int Red, int Blue) CountDominant(byte[]? bgra)
    {
        if (bgra == null) return (0, 0);
        int red = 0, blue = 0;
        for (var i = 0; i + 3 < bgra.Length; i += 4)
        {
            int b = bgra[i], g = bgra[i + 1], r = bgra[i + 2];
            if (r > g + 40 && r > b + 40) red++;
            else if (b > g + 40 && b > r + 40) blue++;
        }
        return (red, blue);
    }

    private static double MeanAbsoluteDifference(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return double.MaxValue;
        long sum = 0;
        for (var i = 0; i < a.Length; i++) sum += Math.Abs(a[i] - b[i]);
        return (double)sum / a.Length;
    }

    private static AppSettings BuildSettings(SurfaceStyle surface) => new(
        new Theme(
            DarkMode: true,
            AccentColor: null,
            OpacityLevel: 0.8,
            Monochrome: false,
            UseNativeFrame: false,
            FontFamily: "Segoe UI",
            Surface: surface),
        Templates: [],
        new Layout(GridMode.Manual, true, false, true, false),
        new Dimensions(72, 8, 16),
        new Region("zh-Hans"),
        RunOnStartup: false,
        IgnoreUpdate: null);

    private static object? ReadField(object target, string name) => target
        .GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
        .GetValue(target);

    private static (bool IsAcrylic, bool IsLiquidGlass, bool IsSolid) ResolveTheme(object clock)
    {
        var method = clock.GetType().GetMethod("ResolveEffectiveTheme", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return ((bool, bool, bool))method.Invoke(clock, null)!;
    }

    private static void Check(string what, bool ok)
    {
        Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {what}");
        if (!ok) failures++;
    }

    private sealed class StubSettings(AppSettings value) : IAppSettingsProvider
    {
        public event DataChangedEvent<AppSettings>? DataChanging;
        public event DataChangedEvent<AppSettings>? DataChanged;

        public AppSettings Get() => value;

        public void Save(AppSettings data)
        {
            var old = value;
            DataChanging?.Invoke(this, old, data);
            value = data;
            DataChanged?.Invoke(this, old, data);
        }
    }

    private sealed class StubLayout : IWidgetLayoutProvider
    {
        public event DataChangedEvent<WidgetLayout>? DataChanging;
        public event DataChangedEvent<WidgetLayout>? DataChanged;

        public string ScreenId { get; set; } = "probe";

        public WidgetLayout Get() => new("Clock", "FramelessDigital", 0, 0, 368, 184, null);

        public void Save(WidgetLayout data)
        {
            DataChanging?.Invoke(this, data, data);
            DataChanged?.Invoke(this, data, data);
        }

        public void Remove() { }
    }
}
