using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Calendar.Views.Controls;

/// <summary>
/// The "today" marker of the month grid: one filled disc with the day number punched out of it
/// (镂空), so the number reads as a hole in the disc and the surface behind the card — the glass,
/// with whatever wallpaper it is refracting — shows through it.
/// <para>
/// A knock-out cannot be expressed with a text element drawn on top of a filled ellipse, so the
/// disc is drawn as a single geometry: the ellipse with the glyph outlines subtracted
/// (<see cref="GeometryCombineMode.Exclude"/>). With <see cref="Hollow"/> off the control draws
/// the plain filled disc instead and the template's text block paints the number on top, which is
/// the historic look.
/// </para>
/// </summary>
public class HollowTodayMark : Control
{
    /// <summary>Draw the marker at all (the cell belongs to today).</summary>
    public static readonly StyledProperty<bool> IsTodayProperty =
        AvaloniaProperty.Register<HollowTodayMark, bool>(nameof(IsToday));

    /// <summary>Punch the number out of the disc instead of painting it on top.</summary>
    public static readonly StyledProperty<bool> HollowProperty =
        AvaloniaProperty.Register<HollowTodayMark, bool>(nameof(Hollow), true);

    /// <summary>The day number to punch out.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<HollowTodayMark, string?>(nameof(Text));

    /// <summary>Color of the disc (the number itself is transparent).</summary>
    public static readonly StyledProperty<IBrush?> DotBrushProperty =
        AvaloniaProperty.Register<HollowTodayMark, IBrush?>(nameof(DotBrush));

    /// <summary>Font size the punched-out number is built at, in the control's own coordinates.</summary>
    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<HollowTodayMark, double>(nameof(GlyphSize), 60);

    /// <summary>Weight the punched-out number is built at (the grid draws bold day numbers).</summary>
    public static readonly StyledProperty<FontWeight> GlyphWeightProperty =
        AvaloniaProperty.Register<HollowTodayMark, FontWeight>(nameof(GlyphWeight), FontWeight.Bold);

    /// <summary>Typeface the punched-out number is built with (null = the app default).</summary>
    public static readonly StyledProperty<FontFamily?> GlyphFontFamilyProperty =
        AvaloniaProperty.Register<HollowTodayMark, FontFamily?>(nameof(GlyphFontFamily));

    static HollowTodayMark()
    {
        AffectsRender<HollowTodayMark>(
            IsTodayProperty, HollowProperty, TextProperty,
            DotBrushProperty, GlyphSizeProperty, GlyphWeightProperty, GlyphFontFamilyProperty);
    }

    public bool IsToday
    {
        get => GetValue(IsTodayProperty);
        set => SetValue(IsTodayProperty, value);
    }

    public bool Hollow
    {
        get => GetValue(HollowProperty);
        set => SetValue(HollowProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public IBrush? DotBrush
    {
        get => GetValue(DotBrushProperty);
        set => SetValue(DotBrushProperty, value);
    }

    public double GlyphSize
    {
        get => GetValue(GlyphSizeProperty);
        set => SetValue(GlyphSizeProperty, value);
    }

    public FontWeight GlyphWeight
    {
        get => GetValue(GlyphWeightProperty);
        set => SetValue(GlyphWeightProperty, value);
    }

    public FontFamily? GlyphFontFamily
    {
        get => GetValue(GlyphFontFamilyProperty);
        set => SetValue(GlyphFontFamilyProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (!IsToday) return;

        var side = Math.Min(Bounds.Width, Bounds.Height);
        if (side <= 0) return;

        var brush = DotBrush;
        if (brush == null) return;

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = side / 2;

        if (!Hollow)
        {
            // Historic look: the template's text block paints the number on the disc.
            context.DrawEllipse(brush, null, center, radius, radius);
            return;
        }

        var disc = new EllipseGeometry(new Rect(center.X - radius, center.Y - radius, side, side));
        var glyph = BuildNumberGeometry(side);

        // Subtract the numeral from the disc. The holes stay unfilled, so the card's own
        // background (glass or solid) is what shows inside the number.
        Geometry marker = glyph == null
            ? disc
            : new CombinedGeometry(GeometryCombineMode.Exclude, disc, glyph);

        context.DrawGeometry(brush, null, marker);
    }

    /// <summary>
    /// The outline of the day number, centered in the marker's own box — the same centering the
    /// text block uses, so the punched-out number sits exactly where the painted one would.
    /// </summary>
    private Geometry? BuildNumberGeometry(double side)
    {
        if (string.IsNullOrEmpty(Text)) return null;

        var typeface = new Typeface(GlyphFontFamily ?? FontFamily.Default, FontStyle.Normal, GlyphWeight);
        var formatted = new FormattedText(
            Text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            GlyphSize,
            Brushes.Black);

        var origin = new Point(
            (Bounds.Width - formatted.Width) / 2,
            (Bounds.Height - formatted.Height) / 2);

        return formatted.BuildGeometry(origin);
    }
}
