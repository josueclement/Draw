using System.Collections.Generic;
using Draw.Model.Connectors;
using Draw.Model.Nodes;
using Draw.Model.Styling;

namespace Draw.Model.Documents;

/// <summary>The root persisted unit: one diagram per file (<c>.draw</c>).</summary>
public sealed class DiagramDocument
{
    public int SchemaVersion { get; set; } = DocumentSchema.CurrentVersion;

    public DiagramType DiagramType { get; set; } = DiagramType.Freeform;

    public string? Title { get; set; }

    /// <summary>Whether the canvas grid is drawn for this diagram. Per-document and persisted; a file
    /// written before this field existed omits the key and so opens with the grid shown (the default).</summary>
    public bool ShowGrid { get; set; } = true;

    public List<NodeBase> Nodes { get; set; } = new();

    public List<Connector> Connectors { get; set; } = new();

    public ShapeStyle DefaultShapeStyle { get; set; } = ShapeStyle.CreateDefault();

    public DocumentMetadata Metadata { get; set; } = new();

    public static DiagramDocument CreateEmpty(DiagramType type) => new() { DiagramType = type };
}
