namespace Draw.Model.Nodes;

/// <summary>A UML package — a folder-tab box that groups related elements; carries a single title.</summary>
public sealed class PackageNode : NodeBase
{
    public string Title { get; set; } = string.Empty;

    public override NodeBase Clone()
    {
        PackageNode copy = new() { Title = Title };
        CopyBaseTo(copy);
        return copy;
    }
}
