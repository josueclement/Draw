using System;
using System.Collections.Generic;
using Draw.Diagramming.Geometry;
using Draw.Model.Connectors;
using Draw.Model.Primitives;

namespace Draw.Diagramming.Routing;

/// <summary>
/// Axis-aligned (elbow) routing. With no bend points it produces a Z-elbow between the
/// facing boundaries; with bend points it routes orthogonally through them.
/// </summary>
public sealed class OrthogonalRouter : IConnectorRouteStrategy
{
    public RouteStyle Style => RouteStyle.Orthogonal;

    public ConnectorRoute Route(ConnectorRouteRequest request)
    {
        Point2D sourceCenter = request.SourceBounds.Center;
        Point2D targetCenter = request.TargetBounds.Center;

        if (request.BendPoints.Count == 0)
        {
            Point2D source = ShapeBoundary.IntersectFromCenter(request.SourceKind, request.SourceBounds, targetCenter);
            Point2D target = ShapeBoundary.IntersectFromCenter(request.TargetKind, request.TargetBounds, sourceCenter);

            // Overlapping nodes make the elbow self-cross; fall back to a direct segment.
            if (request.SourceBounds.IntersectsWith(request.TargetBounds))
            {
                return ConnectorRoute.Polyline(RouteHelpers.Dedupe(new List<Point2D> { source, target }));
            }

            return ConnectorRoute.Polyline(RouteHelpers.Dedupe(BuildElbow(source, target, sourceCenter, targetCenter)));
        }

        Point2D src = ShapeBoundary.IntersectFromCenter(request.SourceKind, request.SourceBounds, request.BendPoints[0]);
        Point2D tgt = ShapeBoundary.IntersectFromCenter(request.TargetKind, request.TargetBounds, request.BendPoints[request.BendPoints.Count - 1]);

        List<Point2D> sequence = new() { src };
        sequence.AddRange(request.BendPoints);
        sequence.Add(tgt);
        return ConnectorRoute.Polyline(RouteHelpers.Dedupe(Orthogonalize(sequence)));
    }

    private static List<Point2D> BuildElbow(Point2D source, Point2D target, Point2D sourceCenter, Point2D targetCenter)
    {
        double dx = targetCenter.X - sourceCenter.X;
        double dy = targetCenter.Y - sourceCenter.Y;

        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            double midX = (source.X + target.X) / 2;
            return new List<Point2D>
            {
                source,
                new(midX, source.Y),
                new(midX, target.Y),
                target,
            };
        }

        double midY = (source.Y + target.Y) / 2;
        return new List<Point2D>
        {
            source,
            new(source.X, midY),
            new(target.X, midY),
            target,
        };
    }

    private static List<Point2D> Orthogonalize(IReadOnlyList<Point2D> sequence)
    {
        List<Point2D> result = new() { sequence[0] };
        for (int i = 1; i < sequence.Count; i++)
        {
            Point2D previous = result[result.Count - 1];
            Point2D current = sequence[i];
            if (Math.Abs(previous.X - current.X) > 1e-6 && Math.Abs(previous.Y - current.Y) > 1e-6)
            {
                result.Add(new Point2D(current.X, previous.Y));
            }

            result.Add(current);
        }

        return result;
    }
}
