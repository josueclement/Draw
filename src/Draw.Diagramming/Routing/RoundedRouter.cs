using System;
using System.Collections.Generic;
using Draw.Diagramming.Geometry;
using Draw.Model.Connectors;
using Draw.Model.Primitives;

namespace Draw.Diagramming.Routing;

/// <summary>
/// A smooth curve averaged from the bend points. The curve passes through the two boundary
/// attachment points and the midpoints between consecutive bend points, using each bend point as a
/// pull-handle (classic midpoint-quadratic smoothing, emitted as cubic segments). With no bend
/// points it bows outward from each shape along its boundary normal as a gentle S-curve.
/// </summary>
public sealed class RoundedRouter : IConnectorRouteStrategy
{
    public RouteStyle Style => RouteStyle.Rounded;

    public ConnectorRoute Route(ConnectorRouteRequest request)
    {
        Point2D sourceCenter = request.SourceBounds.Center;
        Point2D targetCenter = request.TargetBounds.Center;

        // No bend points: a gentle S-curve between the two boundary points.
        if (request.BendPoints.Count == 0)
        {
            Point2D src = ResolveSource(request, targetCenter);
            Point2D tgt = ResolveTarget(request, sourceCenter);
            double handle = Math.Max(20d, (tgt - src).Length * 0.4);
            Point2D outwardSource = RouteHelpers.SafeOutward(src - sourceCenter, tgt - src);
            Point2D outwardTarget = RouteHelpers.SafeOutward(tgt - targetCenter, src - tgt);
            CubicSegment curve = new(src + (outwardSource * handle), tgt + (outwardTarget * handle), tgt);
            return ConnectorRoute.PolyCubic(src, new[] { curve });
        }

        // Attach toward the first/last bend point (like the straight/orthogonal styles).
        Point2D source = ResolveSource(request, request.BendPoints[0]);
        Point2D target = ResolveTarget(request, request.BendPoints[request.BendPoints.Count - 1]);

        List<Point2D> points = new() { source };
        points.AddRange(request.BendPoints);
        points.Add(target);
        List<Point2D> pts = RouteHelpers.Dedupe(points);

        // A degenerate sequence (bend points collapsed onto the endpoints) has nothing to round.
        if (pts.Count < 3)
        {
            return ConnectorRoute.PolyCubic(pts[0], new[] { ToCubic(pts[0], pts[0], pts[pts.Count - 1]) });
        }

        // Midpoint-quadratic smoothing: ride the midpoints, pull toward each interior point.
        int n = pts.Count;
        List<CubicSegment> segments = new(n - 1);
        Point2D current = pts[0];
        for (int i = 1; i <= n - 3; i++)
        {
            Point2D end = Midpoint(pts[i], pts[i + 1]);
            segments.Add(ToCubic(current, pts[i], end));
            current = end;
        }

        segments.Add(ToCubic(current, pts[n - 2], pts[n - 1]));
        return ConnectorRoute.PolyCubic(pts[0], segments);
    }

    private static Point2D ResolveSource(ConnectorRouteRequest request, Point2D toward)
        => request.SourceAnchor is { } anchor
            ? ShapeBoundary.ResolveAnchor(request.SourceKind, request.SourceBounds, anchor)
            : ShapeBoundary.IntersectFromCenter(request.SourceKind, request.SourceBounds, toward);

    private static Point2D ResolveTarget(ConnectorRouteRequest request, Point2D toward)
        => request.TargetAnchor is { } anchor
            ? ShapeBoundary.ResolveAnchor(request.TargetKind, request.TargetBounds, anchor)
            : ShapeBoundary.IntersectFromCenter(request.TargetKind, request.TargetBounds, toward);

    // Exact quadratic (start S, control Q, end E) → cubic conversion.
    private static CubicSegment ToCubic(Point2D s, Point2D q, Point2D e)
        => new(s + ((q - s) * (2d / 3d)), e + ((q - e) * (2d / 3d)), e);

    private static Point2D Midpoint(Point2D a, Point2D b) => (a + b) * 0.5d;
}
