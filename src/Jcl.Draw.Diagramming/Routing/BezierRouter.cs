using System;
using Jcl.Draw.Diagramming.Geometry;
using Jcl.Draw.Model.Connectors;
using Jcl.Draw.Model.Primitives;

namespace Jcl.Draw.Diagramming.Routing;

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

        Point2D source = ShapeBoundary.IntersectFromCenter(request.SourceKind, request.SourceBounds, targetCenter);
        Point2D target = ShapeBoundary.IntersectFromCenter(request.TargetKind, request.TargetBounds, sourceCenter);

        double handle = Math.Max(20d, (target - source).Length * 0.4);
        Point2D outwardSource = (source - sourceCenter).Normalized();
        Point2D outwardTarget = (target - targetCenter).Normalized();

        Point2D control1 = source + (outwardSource * handle);
        Point2D control2 = target + (outwardTarget * handle);

        return ConnectorRoute.Bezier(source, control1, control2, target);
    }
}
