namespace Draw.Model.Styling;

/// <summary>Fill, stroke, font and text alignment for a shape node.</summary>
public sealed class ShapeStyle
{
    // Default fill: Carbon light-theme "surface" tone (#EBEDF0). The concrete fallback used when Fill is
    // null and no theme brush is available (e.g. export). Since v2, "follow the theme default" is
    // represented explicitly by a null Fill — not by value-equality on this constant.
    public static readonly ArgbColor DefaultFill = new(0xFF, 0xEB, 0xED, 0xF0);

    /// <summary>The fill colour, or <c>null</c> to follow the active theme's default node fill.</summary>
    public ArgbColor? Fill { get; set; }

    public StrokeStyle Stroke { get; set; } = new();

    public FontSpec Font { get; set; } = new();

    public TextAlignment TextAlignment { get; set; } = TextAlignment.Center;

    public TextVerticalAlignment VerticalTextAlignment { get; set; } = TextVerticalAlignment.Center;

    /// <summary>Id of the quick-palette swatch this fill/stroke/text came from, or null when the colours
    /// are the default sentinel or a custom (hand-edited) value. When set, the App layer resolves the
    /// rendered colours from the swatch's active-theme variant, so the node recolours on theme toggle.</summary>
    public string? PaletteId { get; set; }

    public static ShapeStyle CreateDefault() => new();

    public ShapeStyle Clone() => new()
    {
        Fill = Fill,
        Stroke = Stroke.Clone(),
        Font = Font.Clone(),
        TextAlignment = TextAlignment,
        VerticalTextAlignment = VerticalTextAlignment,
        PaletteId = PaletteId,
    };
}
