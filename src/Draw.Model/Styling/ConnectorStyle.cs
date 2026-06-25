namespace Draw.Model.Styling;

/// <summary>Stroke and label font for a connector. Fully rendered starting in Phase 2.</summary>
public sealed class ConnectorStyle
{
    /// <summary>Default connector (and mind-map branch) stroke: the design system's neutral "Gray"
    /// swatch stroke, deliberately distinct from node outlines, which keep the accent blue
    /// (<see cref="StrokeStyle.DefaultColor"/>).</summary>
    public static readonly ArgbColor DefaultStrokeColor = new(0xFF, 0x9A, 0x9A, 0xA0);

    public StrokeStyle Stroke { get; set; } = new() { Color = DefaultStrokeColor };

    public FontSpec Font { get; set; } = new();

    /// <summary>Id of the quick-palette swatch this stroke/label colour came from, or null for default/
    /// custom. When set, the App layer resolves the colour from the swatch's active-theme variant.</summary>
    public string? PaletteId { get; set; }

    public ConnectorStyle Clone() => new()
    {
        Stroke = Stroke.Clone(),
        Font = Font.Clone(),
        PaletteId = PaletteId,
    };
}
