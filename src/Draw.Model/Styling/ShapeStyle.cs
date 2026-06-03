namespace Draw.Model.Styling;

/// <summary>Fill, stroke, font and text alignment for a shape node.</summary>
public sealed class ShapeStyle
{
    // Default fill: Carbon light-theme "surface" tone (#EBEDF0). Doubles as the sentinel the App
    // layer uses to detect an un-customised fill and swap in the theme-aware brush (dark: #2B2D30).
    public static readonly ArgbColor DefaultFill = new(0xFF, 0xEB, 0xED, 0xF0);

    public ArgbColor Fill { get; set; } = DefaultFill;

    public StrokeStyle Stroke { get; set; } = new();

    public FontSpec Font { get; set; } = new();

    public TextAlignment TextAlignment { get; set; } = TextAlignment.Center;

    public TextVerticalAlignment VerticalTextAlignment { get; set; } = TextVerticalAlignment.Center;

    public static ShapeStyle CreateDefault() => new();

    public ShapeStyle Clone() => new()
    {
        Fill = Fill,
        Stroke = Stroke.Clone(),
        Font = Font.Clone(),
        TextAlignment = TextAlignment,
        VerticalTextAlignment = VerticalTextAlignment,
    };
}
