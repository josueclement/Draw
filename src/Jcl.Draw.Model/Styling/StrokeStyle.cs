namespace Jcl.Draw.Model.Styling;

/// <summary>Outline/line styling shared by shapes and connectors.</summary>
public sealed class StrokeStyle
{
    public ArgbColor Color { get; set; } = ArgbColor.Black;

    public double Thickness { get; set; } = 1.5d;

    public DashStyle Dash { get; set; } = DashStyle.Solid;

    public StrokeStyle Clone() => new()
    {
        Color = Color,
        Thickness = Thickness,
        Dash = Dash,
    };
}
