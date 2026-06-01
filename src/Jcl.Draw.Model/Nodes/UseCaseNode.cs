namespace Jcl.Draw.Model.Nodes;

/// <summary>A UML use case — an ellipse with centered text.</summary>
public sealed class UseCaseNode : NodeBase
{
    public string Text { get; set; } = string.Empty;

    public override NodeBase Clone()
    {
        UseCaseNode copy = new() { Text = Text };
        CopyBaseTo(copy);
        return copy;
    }
}
