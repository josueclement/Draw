namespace Draw.Model.Nodes;

/// <summary>A UML component — a «component»-stereotyped box with port tabs; carries a single name.</summary>
public sealed class ComponentNode : NodeBase
{
    public string Name { get; set; } = string.Empty;

    public override NodeBase Clone()
    {
        ComponentNode copy = new() { Name = Name };
        CopyBaseTo(copy);
        return copy;
    }
}
