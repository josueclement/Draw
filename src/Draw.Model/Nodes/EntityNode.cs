using System.Collections.Generic;
using System.Linq;

namespace Draw.Model.Nodes;

/// <summary>A database entity (table) drawn as a titled box over a flat list of columns.</summary>
public sealed class EntityNode : NodeBase
{
    public string Name { get; set; } = string.Empty;

    public List<EntityColumn> Columns { get; set; } = new();

    public override NodeBase Clone()
    {
        EntityNode copy = new()
        {
            Name = Name,
            Columns = Columns.Select(c => c.Clone()).ToList(),
        };
        CopyBaseTo(copy);
        return copy;
    }
}
