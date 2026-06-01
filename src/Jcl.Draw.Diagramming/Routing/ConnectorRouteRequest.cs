using System;
using System.Collections.Generic;
using Jcl.Draw.Model.Connectors;
using Jcl.Draw.Model.Nodes;
using Jcl.Draw.Model.Primitives;

namespace Jcl.Draw.Diagramming.Routing;

/// <summary>Inputs needed to route a connector between two nodes.</summary>
public sealed class ConnectorRouteRequest
{
    public ConnectorRouteRequest(
        ShapeKind sourceKind,
        Rect2D sourceBounds,
        ShapeKind targetKind,
        Rect2D targetBounds,
        RouteStyle style,
        IReadOnlyList<Point2D>? bendPoints = null)
    {
        SourceKind = sourceKind;
        SourceBounds = sourceBounds;
        TargetKind = targetKind;
        TargetBounds = targetBounds;
        Style = style;
        BendPoints = bendPoints ?? Array.Empty<Point2D>();
    }

    public ShapeKind SourceKind { get; }

    public Rect2D SourceBounds { get; }

    public ShapeKind TargetKind { get; }

    public Rect2D TargetBounds { get; }

    public RouteStyle Style { get; }

    public IReadOnlyList<Point2D> BendPoints { get; }
}
