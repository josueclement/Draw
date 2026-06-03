using System;
using System.Collections.Generic;
using Draw.Model.Connectors;
using Draw.Model.Nodes;
using Draw.Model.Primitives;

namespace Draw.Diagramming.Routing;

/// <summary>Inputs needed to route a connector between two nodes.</summary>
public sealed class ConnectorRouteRequest
{
    public ConnectorRouteRequest(
        ShapeKind sourceKind,
        Rect2D sourceBounds,
        ShapeKind targetKind,
        Rect2D targetBounds,
        RouteStyle style,
        IReadOnlyList<Point2D>? bendPoints = null,
        Point2D? sourceAnchor = null,
        Point2D? targetAnchor = null)
    {
        SourceKind = sourceKind;
        SourceBounds = sourceBounds;
        TargetKind = targetKind;
        TargetBounds = targetBounds;
        Style = style;
        BendPoints = bendPoints ?? Array.Empty<Point2D>();
        SourceAnchor = sourceAnchor;
        TargetAnchor = targetAnchor;
    }

    public ShapeKind SourceKind { get; }

    public Rect2D SourceBounds { get; }

    public ShapeKind TargetKind { get; }

    public Rect2D TargetBounds { get; }

    public RouteStyle Style { get; }

    public IReadOnlyList<Point2D> BendPoints { get; }

    /// <summary>Forced source attachment as a relative (u,v) point in the source bounds; null = automatic.</summary>
    public Point2D? SourceAnchor { get; }

    /// <summary>Forced target attachment as a relative (u,v) point in the target bounds; null = automatic.</summary>
    public Point2D? TargetAnchor { get; }
}
