namespace Jcl.Draw.Model.Nodes;

/// <summary>A basic shape primitive carrying optional centered text.</summary>
public sealed class ShapeNode : NodeBase
{
    public ShapeKind Kind { get; set; } = ShapeKind.Rectangle;

    public string Text { get; set; } = string.Empty;

    /// <summary>Corner radius used only when <see cref="Kind"/> is <see cref="ShapeKind.RoundedRectangle"/>.</summary>
    public double CornerRadius { get; set; } = 12d;

    public override NodeBase Clone()
    {
        ShapeNode copy = new()
        {
            Kind = Kind,
            Text = Text,
            CornerRadius = CornerRadius,
        };
        CopyBaseTo(copy);
        return copy;
    }
}
