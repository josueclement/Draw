using System.Collections.Generic;
using Draw.Diagramming.Geometry;
using Draw.Model.Connectors;
using Draw.Model.Primitives;

namespace Draw.Diagramming.Routing;

/// <summary>Direct line from source boundary to target boundary, through any bend points.</summary>
public sealed class StraightRouter : IConnectorRouteStrategy
{
    public RouteStyle Style => RouteStyle.Straight;

    public ConnectorRoute Route(ConnectorRouteRequest request)
    {
        Point2D sourceCenter = request.SourceBounds.Center;
        Point2D targetCenter = request.TargetBounds.Center;

        Point2D firstToward = request.BendPoints.Count > 0 ? request.BendPoints[0] : targetCenter;
        Point2D lastToward = request.BendPoints.Count > 0 ? request.BendPoints[request.BendPoints.Count - 1] : sourceCenter;

        Point2D source = ShapeBoundary.IntersectFromCenter(request.SourceKind, request.SourceBounds, firstToward);
        Point2D target = ShapeBoundary.IntersectFromCenter(request.TargetKind, request.TargetBounds, lastToward);

        List<Point2D> points = new() { source };
        points.AddRange(request.BendPoints);
        points.Add(target);

        return ConnectorRoute.Polyline(RouteHelpers.Dedupe(points));
    }
}
