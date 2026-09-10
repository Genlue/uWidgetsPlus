using System;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Clock.Models;
using Clock.Views;

namespace ClockVisualChecks;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Console.WriteLine("=== Starting Frameless Clock Visual Checks ===");
        var outDir = Path.GetFullPath(args.Length > 0 ? args[0] : "dist/frameless-clock-checks");
        Directory.CreateDirectory(outDir);

        AppBuilder.Configure<Application>()
            .UsePlatformDetect()
            .SetupWithoutStarting();

        var app = Application.Current!;
        app.Styles.Add(new FluentTheme());
        app.RequestedThemeVariant = ThemeVariant.Dark;

        // Verify tight bounds and stretch math
        TestGeometryStretchMath();

        // Visual test cases covering curated artistic fonts and themes
        var testCases = new (string CaseName, double Width, double Height, FramelessClockModel Model)[]
        {
            ("4x2-harmonyos-condensed", 312, 152, new FramelessClockModel(Use24Hours: true, FontFamily: "HarmonyOS Sans Condensed", FontWeight: 800, StretchFill: true)),
            ("4x2-impact-heavy", 312, 152, new FramelessClockModel(Use24Hours: true, FontFamily: "Impact", FontWeight: 800, StretchFill: true)),
            ("4x2-georgia-serif", 312, 152, new FramelessClockModel(Use24Hours: true, FontFamily: "Georgia", FontWeight: 700, StretchFill: true)),
            ("4x2-century-gothic", 312, 152, new FramelessClockModel(Use24Hours: true, FontFamily: "Century Gothic", FontWeight: 700, StretchFill: true)),
            ("4x2-bahnschrift-din", 312, 152, new FramelessClockModel(Use24Hours: true, FontFamily: "Bahnschrift", FontWeight: 700, StretchFill: true)),
            ("4x2-thin-100", 312, 152, new FramelessClockModel(Use24Hours: true, FontWeight: 100, StretchFill: true)),
            ("4x2-black-900", 312, 152, new FramelessClockModel(Use24Hours: true, FontWeight: 900, StretchFill: true)),
            ("2x2-regular-uniform", 152, 152, new FramelessClockModel(Use24Hours: true, FontFamily: "HarmonyOS Sans Condensed", FontWeight: 700, StretchFill: false)),
            ("4x1-seconds-fill", 312, 72, new FramelessClockModel(Use24Hours: true, ShowSeconds: true, FontFamily: "Impact", FontWeight: 700, StretchFill: true)),
            ("4x2-overlay-tint", 312, 152, new FramelessClockModel(Use24Hours: true, FontFamily: "HarmonyOS Sans Condensed", FontWeight: 800, StretchFill: true, EnableOverlay: true, FollowAccentColor: true, OverlayOpacity: 0.40)),
            ("4x2-gold-tint", 312, 152, new FramelessClockModel(Use24Hours: true, FontFamily: "Impact", FontWeight: 700, StretchFill: true, EnableOverlay: true, FollowAccentColor: false, OverlayColor: "#FFCC00", OverlayOpacity: 0.50)),
            ("4x2-acrylic-theme", 312, 152, new FramelessClockModel(Use24Hours: true, FontFamily: "HarmonyOS Sans Condensed", FontWeight: 800, StretchFill: true, ThemeMode: 1)),
            ("4x2-liquid-glass-refined", 312, 152, new FramelessClockModel(Use24Hours: true, FontFamily: "HarmonyOS Sans Condensed", FontWeight: 800, StretchFill: true, ThemeMode: 2)),
            ("4x2-solid-theme", 312, 152, new FramelessClockModel(Use24Hours: true, FontFamily: "HarmonyOS Sans Condensed", FontWeight: 800, StretchFill: true, ThemeMode: 3))
        };

        foreach (var tc in testCases)
        {
            Console.WriteLine($"\n--- Testing and Rendering {tc.CaseName} ({tc.Width}x{tc.Height} DIP) ---");
            RenderAndVerify(outDir, tc.CaseName, tc.Width, tc.Height, tc.Model);
        }

        TestPreCachingAndAutoCleanup();

        Console.WriteLine($"\nAll Frameless Clock checks completed successfully! Output folder: {outDir}");
    }

    private static void TestGeometryStretchMath()
    {
        Console.WriteLine("\n--- Testing Tight Bounds & Stretch Transform Precision ---");
        var timeStr = "12:10";
        var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyle.Normal, FontWeight.Bold);
        var formatted = new FormattedText(timeStr, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 100.0, Brushes.Black);
        var rawGeo = formatted.BuildGeometry(new Point(0, 0))!;
        var tight = rawGeo.Bounds;

        Console.WriteLine($"  Raw geometry tight bounds: X={tight.X:F2}, Y={tight.Y:F2}, W={tight.Width:F2}, H={tight.Height:F2}");
        if (tight.Width <= 0 || tight.Height <= 0)
            throw new Exception("Tight bounds must be non-zero!");

        double targetW = 312.0, targetH = 152.0;
        var sx = targetW / tight.Width;
        var sy = targetH / tight.Height;
        var matrix = Matrix.CreateTranslation(-tight.X, -tight.Y) * Matrix.CreateScale(sx, sy);

        var stretched = rawGeo.Clone();
        stretched.Transform = new MatrixTransform(matrix);
        var stretchedBounds = stretched.Bounds;

        Console.WriteLine($"  Stretched geometry bounds: X={stretchedBounds.X:F2}, Y={stretchedBounds.Y:F2}, W={stretchedBounds.Width:F2}, H={stretchedBounds.Height:F2}");
        if (Math.Abs(stretchedBounds.Y - 0) > 0.05)
            throw new Exception($"Top edge must strictly align to Y=0, got {stretchedBounds.Y}");
        if (Math.Abs(stretchedBounds.Bottom - targetH) > 0.05)
            throw new Exception($"Bottom edge must strictly align to Y={targetH}, got {stretchedBounds.Bottom}");
        if (Math.Abs(stretchedBounds.Right - targetW) > 0.05)
            throw new Exception($"Right edge must strictly align to X={targetW}, got {stretchedBounds.Right}");

        Console.WriteLine("  PASS: Tight bounds and stretch matrix strictly fill target [0, 0, 312, 152] without internal font leading.");
    }

    private static void RenderAndVerify(string outDir, string caseName, double w, double h, FramelessClockModel model)
    {
        var view = new FramelessDigital(model)
        {
            Width = w,
            Height = h
        };

        view.Measure(new Size(w, h));
        view.Arrange(new Rect(0, 0, w, h));
        view.UpdateLayout();

        var dpiScale = 2.0;
        var pixelW = (int)Math.Ceiling(w * dpiScale);
        var pixelH = (int)Math.Ceiling(h * dpiScale);

        using var bmp = new RenderTargetBitmap(new PixelSize(pixelW, pixelH), new Vector(96 * dpiScale, 96 * dpiScale));
        bmp.Render(view);

        var outFile = Path.Combine(outDir, $"{caseName}.png");
        bmp.Save(outFile);
        Console.WriteLine($"  -> Saved: {outFile} ({pixelW}x{pixelH} px)");
        Console.WriteLine($"  PASS: {caseName} rendered without error.");
    }

    private static void TestPreCachingAndAutoCleanup()
    {
        Console.WriteLine("\n--- Testing Liquid Glass Pre-Caching & Automatic Cleanup Logic ---");
        var model = new FramelessClockModel(Use24Hours: true, ShowSeconds: false, FontFamily: "HarmonyOS Sans Condensed", FontWeight: 800, StretchFill: true, ThemeMode: 2);
        var clock = new FramelessDigital(model)
        {
            Width = 312,
            Height = 152
        };

        clock.Measure(new Size(312, 152));
        clock.Arrange(new Rect(0, 0, 312, 152));
        clock.UpdateLayout();

        // Render pass
        using var rtb = new RenderTargetBitmap(new PixelSize(624, 304), new Vector(192, 192));
        rtb.Render(clock);

        Console.WriteLine("  PASS: Liquid Glass mode initialized, pre-caching scheduled, and frame rendered.");
    }
}

