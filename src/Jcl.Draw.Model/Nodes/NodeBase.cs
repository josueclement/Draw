using System;
using System.Text.Json.Serialization;
using Jcl.Draw.Model.Primitives;
using Jcl.Draw.Model.Styling;

namespace Jcl.Draw.Model.Nodes;

/// <summary>
/// Base type for everything placed on the canvas. The JSON type discriminator
/// (<c>$type</c>) keeps the document forward-compatible as later phases add
/// UML class, use-case and ER node types.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ShapeNode), "shape")]
public abstract class NodeBase
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Rect2D Bounds { get; set; }

    public int ZIndex { get; set; }

    public ShapeStyle Style { get; set; } = ShapeStyle.CreateDefault();

    /// <summary>Returns a faithful deep copy, preserving <see cref="Id"/>.</summary>
    public abstract NodeBase Clone();

    /// <summary>Copies the members declared on <see cref="NodeBase"/> into <paramref name="target"/>.</summary>
    protected void CopyBaseTo(NodeBase target)
    {
        target.Id = Id;
        target.Bounds = Bounds;
        target.ZIndex = ZIndex;
        target.Style = Style.Clone();
    }
}
