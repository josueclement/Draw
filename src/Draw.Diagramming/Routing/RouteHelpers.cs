using System;
using System.Collections.Generic;
using Draw.Diagramming.Geometry;
using Draw.Model.Primitives;

namespace Draw.Diagramming.Routing;

internal static class RouteHelpers
{
    private const double Epsilon = 1e-6;

    /// <summary>
    /// Resolves the connector's source endpoint to a point on the source outline: a forced
    /// <see cref="ConnectorRouteRequest.SourceAnchor"/> lands at that relative outline point,
    /// otherwise the boundary crossing on the ray toward <paramref name="toward"/>. Shared by every
    /// route strategy so the anchor-vs-ray decision lives in one place.
    /// </summary>
    public static Point2D ResolveSource(ConnectorRouteRequest request, Point2D toward)
        => request.SourceAnchor is { } anchor
            ? ShapeBoundary.ResolveAnchor(request.SourceKind, request.SourceBounds, anchor)
            : ShapeBoundary.IntersectFromCenter(request.SourceKind, request.SourceBounds, toward);

    /// <summary>Target counterpart of <see cref="ResolveSource"/>.</summary>
    public static Point2D ResolveTarget(ConnectorRouteRequest request, Point2D toward)
        => request.TargetAnchor is { } anchor
            ? ShapeBoundary.ResolveAnchor(request.TargetKind, request.TargetBounds, anchor)
            : ShapeBoundary.IntersectFromCenter(request.TargetKind, request.TargetBounds, toward);

    /// <summary>Removes consecutive duplicate points; guarantees at least two points remain.</summary>
    public static List<Point2D> Dedupe(IReadOnlyList<Point2D> points)
    {
        if (points is null || points.Count == 0)
        {
            throw new ArgumentException("Dedupe requires at least one point.", nameof(points));
        }

        List<Point2D> result = new();
        foreach (Point2D p in points)
        {
            if (result.Count == 0 || result[result.Count - 1].DistanceTo(p) > Epsilon)
            {
                result.Add(p);
            }
        }

        if (result.Count < 2)
        {
            result.Clear();
            result.Add(points[0]);
            result.Add(points[points.Count - 1]);
        }

        return result;
    }

    /// <summary>
    /// A unit "outward" direction: <paramref name="primary"/> if non-degenerate, else
    /// <paramref name="fallback"/>, else +X. Used to bow a curve away from a shape boundary.
    /// </summary>
    public static Point2D SafeOutward(Point2D primary, Point2D fallback)
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
