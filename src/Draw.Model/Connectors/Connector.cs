using System;
using System.Collections.Generic;
using System.Linq;
using Draw.Model.Primitives;
using Draw.Model.Styling;

namespace Draw.Model.Connectors;

/// <summary>
/// A link between two nodes. Persisted as part of the document from Phase 1; the
/// interactive routing/attachment and decoration rendering land in Phase 2.
/// </summary>
public sealed class Connector
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SourceNodeId { get; set; }

    public Guid TargetNodeId { get; set; }

    public RelationshipKind Kind { get; set; } = RelationshipKind.Association;

    /// <summary>Crow's-foot cardinality drawn at the source end (ER relationships). Unspecified = none.</summary>
    public Cardinality SourceCardinality { get; set; } = Cardinality.Unspecified;

    /// <summary>Crow's-foot cardinality drawn at the target end (ER relationships). Unspecified = none.</summary>
    public Cardinality TargetCardinality { get; set; } = Cardinality.Unspecified;

    public RouteStyle Route { get; set; } = RouteStyle.Straight;

    /// <summary>
    /// When true this connector is a mind-map branch: it renders as a filled, depth-scaled tapered
    /// ribbon (thick near the central topic, thinner toward the leaves) instead of a uniform stroked
    /// line, and carries no end decorations. Set when a child is spawned from a topic's '+' button.
    /// Additive/back-compatible: defaults to false for every existing connector.
    /// </summary>
    public bool IsMindMapBranch { get; set; }

    public List<Point2D> BendPoints { get; set; } = new();

    public ConnectorStyle Style { get; set; } = new();

    public string? SourceLabel { get; set; }

    public string? TargetLabel { get; set; }

    public string? CenterLabel { get; set; }

    /// <summary>
    /// Forced source attachment as a relative (u,v) point in [0,1]² of the source node's bounds,
    /// resolved to a point on the shape outline (ray-cast from the centre). Null = automatic.
    /// </summary>
    public Point2D? SourceAnchor { get; set; }

    /// <summary>Forced target attachment; see <see cref="SourceAnchor"/>. Null = automatic.</summary>
    public Point2D? TargetAnchor { get; set; }

    /// <summary>World-unit offset added to the natural source-label position. Null = default.</summary>
    public Point2D? SourceLabelOffset { get; set; }

    /// <summary>World-unit offset added to the natural centre-label position. Null = default.</summary>
    public Point2D? CenterLabelOffset { get; set; }

    /// <summary>World-unit offset added to the natural target-label position. Null = default.</summary>
    public Point2D? TargetLabelOffset { get; set; }

    public Connector Clone() => new()
    {
        Id = Id,
        SourceNodeId = SourceNodeId,
        TargetNodeId = TargetNodeId,
        Kind = Kind,
        SourceCardinality = SourceCardinality,
        TargetCardinality = TargetCardinality,
        Route = Route,
        IsMindMapBranch = IsMindMapBranch,
        BendPoints = BendPoints.ToList(),
        Style = Style.Clone(),
        SourceLabel = SourceLabel,
        TargetLabel = TargetLabel,
        CenterLabel = CenterLabel,
        SourceAnchor = SourceAnchor,
        TargetAnchor = TargetAnchor,
        SourceLabelOffset = SourceLabelOffset,
        CenterLabelOffset = CenterLabelOffset,
        TargetLabelOffset = TargetLabelOffset,
    };
}
