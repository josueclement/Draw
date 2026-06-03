namespace Draw.Model.Styling;

/// <summary>Stroke and label font for a connector. Fully rendered starting in Phase 2.</summary>
public sealed class ConnectorStyle
{
    public StrokeStyle Stroke { get; set; } = new();

    public FontSpec Font { get; set; } = new();

    public ConnectorStyle Clone() => new()
    {
        Stroke = Stroke.Clone(),
        Font = Font.Clone(),
    };
}
