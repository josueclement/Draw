namespace Draw.Model.Nodes;

/// <summary>A UML actor — a stick figure with a name label.</summary>
public sealed class ActorNode : NodeBase
{
    public string Name { get; set; } = string.Empty;

    public override NodeBase Clone()
    {
        ActorNode copy = new() { Name = Name };
        CopyBaseTo(copy);
        return copy;
    }
}
