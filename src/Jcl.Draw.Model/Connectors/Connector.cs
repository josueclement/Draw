using System;
using System.Collections.Generic;
using System.Linq;
using Jcl.Draw.Model.Primitives;
using Jcl.Draw.Model.Styling;

namespace Jcl.Draw.Model.Connectors;

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

    public RouteStyle Route { get; set; } = RouteStyle.Straight;

    public List<Point2D> BendPoints { get; set; } = new();

    public ConnectorStyle Style { get; set; } = new();

    public string? SourceLabel { get; set; }

    public string? TargetLabel { get; set; }

    public string? CenterLabel { get; set; }

    public Connector Clone() => new()
    {
        Id = Id,
        SourceNodeId = SourceNodeId,
        TargetNodeId = TargetNodeId,
        Kind = Kind,
        Route = Route,
        BendPoints = BendPoints.ToList(),
        Style = Style.Clone(),
        SourceLabel = SourceLabel,
        TargetLabel = TargetLabel,
        CenterLabel = CenterLabel,
    };
}
