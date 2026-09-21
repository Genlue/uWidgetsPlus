using System;
using System.IO;
using System.Text;
using System.Threading;

namespace uWidgets.Services;

/// <summary>
/// A tiny, capped trace of the glass material's render decisions.
/// <para>
/// The GPU path cannot be validated offline — Skia's raster backend terminates the process when a
/// runtime shader is drawn, so there is no headless way to see the result — and "the card is blank"
/// has several very different causes (no GPU context, a prepare that never publishes, a shader that
/// fails to bind). This records which of them actually happened, so a report can be diagnosed
/// without guessing. It is written to <see cref="FilePath"/> and stops after <see cref="MaxLines"/>
/// so it can be left on.
/// </para>
/// </summary>
internal static class GlassDiagnostics
{
    private const int MaxLines = 800;

    /// <summary>Set to false to silence the trace entirely.</summary>
    public static bool Enabled { get; set; } = true;

    private static readonly object Gate = new();
    private static int lines;
    private static string? path;
    private static long lastNoteAt;

    /// <summary>Where the trace is written, or null when it could not be created.</summary>
    public static string? FilePath
    {
        get
        {
            lock (Gate)
            {
                if (path == null) path = ResolvePath();
                return path;
            }
        }
    }

    private static string? ResolvePath()
    {
        try
        {
            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "uWidgets");
            Directory.CreateDirectory(dir);
            var file = System.IO.Path.Combine(dir, "glass-debug.log");
            File.WriteAllText(file, $"--- uWidgets glass trace, {DateTime.Now:O} ---{Environment.NewLine}");
            lines = 0;
            return file;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Record one render decision with its wall time and the geometry it used. Throttled: the
    /// first frames and anything after a gap are kept, the steady state is sampled.
    /// </summary>
    public static void Note(string message, long startedTick, LiquidGlassRenderer.Frame frame)
    {
        if (!Enabled) return;
        var now = Environment.TickCount64;
        var elapsed = now - startedTick;
        var sinceLast = now - Interlocked.Read(ref lastNoteAt);
        // Keep the first 40 lines, then roughly one per second — enough to see a steady state
        // without writing a file per frame.
        if (lines >= 40 && sinceLast < 1000) return;
        Interlocked.Exchange(ref lastNoteAt, now);
        Write($"{message} in {elapsed}ms · {frame.Width}x{frame.Height}px scale={frame.Scale:F2} " +
              $"desktop={frame.DesktopWidth:F0}x{frame.DesktopHeight:F0} at ({frame.DesktopX:F0},{frame.DesktopY:F0})");
    }

    /// <summary>Record a state transition (first GPU draw, software backend, …). Always kept.</summary>
    public static void Event(string message)
    {
        if (!Enabled) return;
        Write(message);
    }

    /// <summary>Record a failure with its stack. Always kept.</summary>
    public static void Failure(Exception ex)
    {
        if (!Enabled) return;
        Write($"EXCEPTION {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
    }

    private static void Write(string message)
    {
        lock (Gate)
        {
            var target = FilePath;
            if (target == null || lines >= MaxLines) return;
            try
            {
                var builder = new StringBuilder();
                builder.Append(DateTime.Now.ToString("HH:mm:ss.fff")).Append(" [").Append(Environment.CurrentManagedThreadId).Append("] ")
                       .Append(message).Append(Environment.NewLine);
                File.AppendAllText(target, builder.ToString());
                lines++;
                if (lines == MaxLines) File.AppendAllText(target, "--- trace cap reached ---" + Environment.NewLine);
            }
            catch
            {
                // Diagnostics must never be the reason a widget fails to draw.
            }
        }
    }
}
