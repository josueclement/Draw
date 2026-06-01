using System.Collections.Generic;
using Jcl.Draw.Model.Connectors;
using Jcl.Draw.Model.Nodes;
using Jcl.Draw.Model.Styling;

namespace Jcl.Draw.Model.Documents;

/// <summary>The root persisted unit: one diagram per file (<c>.jcld</c>).</summary>
public sealed class DiagramDocument
{
    public int SchemaVersion { get; set; } = DocumentSchema.CurrentVersion;

    public DiagramType DiagramType { get; set; } = DiagramType.Freeform;

    public string? Title { get; set; }

    public List<NodeBase> Nodes { get; set; } = new();

    public List<Connector> Connectors { get; set; } = new();

    public ShapeStyle DefaultShapeStyle { get; set; } = ShapeStyle.CreateDefault();

    public DocumentMetadata Metadata { get; set; } = new();

    public static DiagramDocument CreateEmpty(DiagramType type) => new() { DiagramType = type };
}
