using System;
using Draw.Diagramming.Geometry;
using Draw.Model.Connectors;
using Draw.Model.Primitives;

namespace Draw.Diagramming.Routing;

/// <summary>
/// A single cubic bezier leaving each shape along its outward boundary normal. Bend points are
/// not used by the bezier strategy.
/// </summary>
public sealed class BezierRouter : IConnectorRouteStrategy
{
    public RouteStyle Style => RouteStyle.Bezier;

    public ConnectorRoute Route(ConnectorRouteRequest request)
    {
        Point2D sourceCenter = request.SourceBounds.Center;
        Point2D targetCenter = request.TargetBounds.Center;

        Point2D source = request.SourceAnchor is { } sa
            ? ShapeBoundary.ResolveAnchor(request.SourceKind, request.SourceBounds, sa)
            : ShapeBoundary.IntersectFromCenter(request.SourceKind, request.SourceBounds, targetCenter);
        Point2D target = request.TargetAnchor is { } ta
            ? ShapeBoundary.ResolveAnchor(request.TargetKind, request.TargetBounds, ta)
            : ShapeBoundary.IntersectFromCenter(request.TargetKind, request.TargetBounds, sourceCenter);

        double handle = Math.Max(20d, (target - source).Length * 0.4);
        Point2D outwardSource = SafeOutward(source - sourceCenter, target - source);
        Point2D outwardTarget = SafeOutward(target - targetCenter, source - target);

        Point2D control1 = source + (outwardSource * handle);
        Point2D control2 = target + (outwardTarget * handle);

        return ConnectorRoute.Bezier(source, control1, control2, target);
    }

    private static Point2D SafeOutward(Point2D primary, Point2D fallback)
    {
        Point2D normalized = primary.Normalized();
        if (normalized.Length >= 0.5d)
        {
            return normalized;
        }

        Point2D alternative = fallback.Normalized();
        return alternative.Length >= 0.5d ? alternative : new Point2D(1, 0);
    }
}
