namespace Draw.Model.Nodes;

/// <summary>A UML deployment node — a 3D box representing a physical/execution unit; carries a single name.</summary>
public sealed class DeploymentNode : NodeBase
{
    public string Name { get; set; } = string.Empty;

    public override NodeBase Clone()
    {
        DeploymentNode copy = new() { Name = Name };
        CopyBaseTo(copy);
        return copy;
    }
}
