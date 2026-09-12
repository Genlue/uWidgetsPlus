using System.Reflection;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Clock.Models;
using Clock.Views;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;
using uWidgets.Core.Models.Settings;

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

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
        return failures == 0 ? 0 : 1;
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
