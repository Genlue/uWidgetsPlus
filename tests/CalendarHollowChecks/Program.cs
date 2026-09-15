using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Calendar.Models;
using Calendar.Views;
using Calendar.Views.Controls;
using SkiaSharp;

namespace CalendarHollowChecks;

/// <summary>
/// Verifies the calendar's 镂空 "today" marker: the day number is a hole punched out of the
/// marker disc, so the pixels inside the numeral are transparent (the surface behind the card
/// shows through) while the disc itself stays filled with the configured color.
///
/// The check renders the real <see cref="Month"/> control off-screen at a 4-cell size with a
/// custom today color, then measures the actual pixels:
/// <list type="bullet">
/// <item>hollow on  → the disc exists, and pixels inside its bounds are transparent (the hole);
/// the hole is a numeral, not the whole disc, so it covers only part of the bounding box;</item>
/// <item>hollow off → the disc exists and nothing inside it is transparent (the number is simply
/// painted on top, the historic look).</item>
/// </list>
/// </summary>
class Program
{
    private static int failures;
    private static string outputDir = "dist/calendar-hollow-checks";

    /// <summary>Today marker color used by the check (unambiguous to detect in the render).</summary>
    private const string MarkerHex = "#FF0000";

    private const int Size = 368;

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0) outputDir = args[0];
        Directory.CreateDirectory(outputDir);

        AppBuilder.Configure<Application>().UsePlatformDetect().SetupWithoutStarting();
        Application.Current!.Styles.Add(new FluentTheme());

        Console.WriteLine("=== Calendar 镂空 today-marker checks ===");
        Console.WriteLine($"control rendered at {Size}×{Size} DIP, marker {MarkerHex}");
        Console.WriteLine();

        var hollowPixels = Render(hollow: true, "today-hollow");
        var paintedPixels = Render(hollow: false, "today-painted");

        var hollow = Analyze(hollowPixels);
        var painted = Analyze(paintedPixels);
        Console.WriteLine($"  hollow : disc px {hollow.DiscPixels}, hole px {hollow.HolePixels}, painted-white px {hollow.WhitePixels}");
        Console.WriteLine($"  painted: disc px {painted.DiscPixels}, hole px {painted.HolePixels}, painted-white px {painted.WhitePixels}");
        Console.WriteLine();

        Check("the today marker disc is drawn", hollow.DiscPixels > 100 && painted.DiscPixels > 100);
        Check("镂空: today's number is punched out of the disc", hollow.HolePixels >= 20);
        Check("镂空: the hole is a numeral, not the whole disc",
            hollow.BboxArea > 0 && hollow.HolePixels < hollow.BboxArea * 0.5);
        Check("镂空: nothing is painted on top of the disc", hollow.WhitePixels == 0);
        Check("not hollow: nothing inside the disc is transparent", painted.HolePixels == 0);
        Check("not hollow: the number is painted on the disc instead", painted.WhitePixels >= 20);

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// Render the real calendar view for one hollow setting and decode its pixels. The view is
    /// hosted in a window (not measured bare) so the month grid's item containers are realized —
    /// an ItemsControl without a top-level never generates them.
    /// </summary>
    private static byte[]? Render(bool hollow, string name)
    {
        var model = new MonthCalendarModel(
            DayOfWeek.Monday,
            TodayColorMode: "Custom",
            TodayColorLight: MarkerHex,
            TodayColorDark: MarkerHex,
            HollowTodayNumber: hollow);

        var view = new Month(model) { Width = Size, Height = Size };

        // The widget host renders text with grayscale antialiasing (Widget.axaml.cs sets
        // TextRenderingMode.Antialias); without it Skia may add ClearType subpixel fringes, whose
        // red/blue edges are indistinguishable from the marker colour in the pixel analysis below.
        RenderOptions.SetTextRenderingMode(view, TextRenderingMode.Antialias);

        var host = new Window
        {
            Width = Size,
            Height = Size,
            Content = view,
            SystemDecorations = SystemDecorations.None,
            Background = Brushes.Transparent
        };

        host.Measure(new Size(Size, Size));
        host.Arrange(new Rect(0, 0, Size, Size));
        view.UpdateLayout();

        using var bitmap = new RenderTargetBitmap(new PixelSize(Size, Size), new Vector(96, 96));
        bitmap.Render(view);

        var path = Path.Combine(outputDir, $"{name}.png");
        bitmap.Save(path);
        Console.WriteLine($"  {name}: saved {path}");

        using var decoded = SKBitmap.Decode(path);
        if (decoded == null) return null;
        var pixels = new byte[decoded.ByteCount];
        System.Runtime.InteropServices.Marshal.Copy(decoded.GetPixels(), pixels, 0, pixels.Length);
        return pixels;
    }

    /// <summary>
    /// Marker measurements inside the disc: the disc pixels (the marker color), the transparent
    /// pixels (the punched-out number) and the opaque white pixels (a number painted on top).
    /// The corners of the disc's bounding box fall outside the circle — nothing else paints there
    /// — so only pixels within 92% of the radius are counted.
    /// </summary>
    private static (int DiscPixels, int HolePixels, int WhitePixels, int BboxArea) Analyze(byte[]? bgra)
    {
        if (bgra == null) return (0, 0, 0, 0);

        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        var disc = 0;

        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var i = (y * Size + x) * 4;
                // BGRA — the marker is pure red, so a strict threshold keeps antialiased text
                // fringes out of the disc mask.
                if (bgra[i + 2] < 200 || bgra[i + 1] > 60 || bgra[i] > 60 || bgra[i + 3] < 200) continue;

                disc++;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < 0) return (0, 0, 0, 0);

        var bboxArea = (maxX - minX + 1) * (maxY - minY + 1);
        var centerX = (minX + maxX) / 2.0;
        var centerY = (minY + maxY) / 2.0;
        var radius = (maxX - minX + 1) / 2.0;
        var innerRadius = radius * 0.92;

        var holes = 0;
        var whites = 0;
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var dx = x - centerX;
                var dy = y - centerY;
                if (dx * dx + dy * dy > innerRadius * innerRadius) continue;

                var i = (y * Size + x) * 4;
                if (bgra[i + 3] < 40) holes++;
                else if (bgra[i] > 200 && bgra[i + 1] > 200 && bgra[i + 2] > 200) whites++;
            }
        }

        return (disc, holes, whites, bboxArea);
    }

    private static void Check(string what, bool ok)
    {
        Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {what}");
        if (!ok) failures++;
    }
}
