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

    /// <summary>Whether the canvas grid is drawn for this diagram. Per-document and persisted. The default
    /// is hidden, so new diagrams start grid-less; files written before this field existed omit the key
    /// and therefore also open without the grid.</summary>
    public bool ShowGrid { get; set; }

    public List<NodeBase> Nodes { get; set; } = new();

    public List<Connector> Connectors { get; set; } = new();

    public ShapeStyle DefaultShapeStyle { get; set; } = ShapeStyle.CreateDefault();

    public DocumentMetadata Metadata { get; set; } = new();

    public static DiagramDocument CreateEmpty(DiagramType type) => new() { DiagramType = type };
}
