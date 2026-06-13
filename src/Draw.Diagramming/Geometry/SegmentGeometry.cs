using System;
using System.Collections.Generic;
using Draw.Model.Primitives;

namespace Draw.Diagramming.Geometry;

/// <summary>
/// Point-to-segment / point-to-polyline distance math, shared by connector hit-testing and segment
/// picking. Pure and UI-agnostic (operates on <see cref="Point2D"/>), so it is unit-tested here and
/// reused by the App layer instead of being duplicated per view model.
/// </summary>
public static class SegmentGeometry
{
    /// <summary>Shortest distance from <paramref name="point"/> to the segment a→b (clamped to the
    /// segment). A degenerate (zero-length) segment falls back to the distance to its start.</summary>
    public static double DistanceToSegment(Point2D point, Point2D a, Point2D b)
    {
        Point2D ab = b - a;
        double lengthSquared = (ab.X * ab.X) + (ab.Y * ab.Y);
        if (lengthSquared <= GeometryTolerance.LengthSquared)
        {
            return point.DistanceTo(a);
        }

        double t = (((point.X - a.X) * ab.X) + ((point.Y - a.Y) * ab.Y)) / lengthSquared;
        t = Math.Clamp(t, 0d, 1d);
        Point2D projection = new(a.X + (t * ab.X), a.Y + (t * ab.Y));
        return point.DistanceTo(projection);
    }

    /// <summary>Shortest distance from <paramref name="point"/> to the polyline through
    /// <paramref name="points"/> (positive infinity for fewer than two points).</summary>
    public static double DistanceToPolyline(Point2D point, IReadOnlyList<Point2D> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        double min = double.PositiveInfinity;
        for (int i = 1; i < points.Count; i++)
        {
            min = Math.Min(min, DistanceToSegment(point, points[i - 1], points[i]));
        }

        return min;
    }

    /// <summary>Index of the polyline segment nearest <paramref name="point"/> — the segment between
    /// <c>points[i]</c> and <c>points[i+1]</c> has index <c>i</c>. Returns 0 for fewer than two points.</summary>
    public static int NearestSegmentIndex(Point2D point, IReadOnlyList<Point2D> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        int best = 0;
        double bestDistance = double.PositiveInfinity;
        for (int i = 1; i < points.Count; i++)
        {
            double distance = DistanceToSegment(point, points[i - 1], points[i]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i - 1;
            }
        }

        return best;
    }
}
