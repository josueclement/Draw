using System.Collections.Generic;
using System.Linq;

namespace Jcl.Draw.Model.Nodes;

/// <summary>A UML classifier (class, interface or enum) drawn as a compartment box.</summary>
public sealed class ClassNode : NodeBase
{
    public ClassNodeKind Kind { get; set; } = ClassNodeKind.Class;

    public string Name { get; set; } = string.Empty;

    public bool IsAbstract { get; set; }

    public List<ClassMember> Members { get; set; } = new();

    public override NodeBase Clone()
    {
        ClassNode copy = new()
        {
            Kind = Kind,
            Name = Name,
            IsAbstract = IsAbstract,
            Members = Members.Select(m => m.Clone()).ToList(),
        };
        CopyBaseTo(copy);
        return copy;
    }
}
