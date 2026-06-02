namespace Jcl.Draw.Model.Styling;

/// <summary>Outline/line styling shared by shapes and connectors.</summary>
public sealed class StrokeStyle
{
    // Default outline (shapes and connectors): Carbon "accent" blue (#3574F0).
    public ArgbColor Color { get; set; } = new(0xFF, 0x35, 0x74, 0xF0);

    public double Thickness { get; set; } = 1.5d;

    public DashStyle Dash { get; set; } = DashStyle.Solid;

    public StrokeStyle Clone() => new()
    {
        Color = Color,
        Thickness = Thickness,
        Dash = Dash,
    };
}
