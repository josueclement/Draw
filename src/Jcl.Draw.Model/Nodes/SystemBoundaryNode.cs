namespace Jcl.Draw.Model.Nodes;

/// <summary>A UML system boundary — a titled rectangle drawn behind the use cases it groups.</summary>
public sealed class SystemBoundaryNode : NodeBase
{
    public string Title { get; set; } = string.Empty;

    public override NodeBase Clone()
    {
        SystemBoundaryNode copy = new() { Title = Title };
        CopyBaseTo(copy);
        return copy;
    }
}
