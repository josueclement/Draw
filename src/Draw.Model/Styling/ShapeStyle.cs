namespace Draw.Model.Styling;

/// <summary>Fill, stroke, font and text alignment for a shape node.</summary>
public sealed class ShapeStyle
{
    // Default fill: Carbon "surface" tone (#EBEDF0) — a soft grey that reads on both light and dark canvases.
    public ArgbColor Fill { get; set; } = new(0xFF, 0xEB, 0xED, 0xF0);

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
