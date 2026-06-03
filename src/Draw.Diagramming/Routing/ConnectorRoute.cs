using System;
using System.Collections.Generic;
using Draw.Model.Primitives;

namespace Draw.Diagramming.Routing;

/// <summary>
/// The computed geometry of a connector: either a polyline (<see cref="Points"/>) or a single
/// cubic bezier. Endpoint direction unit vectors are provided for orienting arrow/diamond/
/// triangle decorations and are guaranteed non-zero (a default is substituted for degenerate routes).
/// </summary>
public sealed class ConnectorRoute
{
    private ConnectorRoute(
        IReadOnlyList<Point2D> points,
        bool isBezier,
        Point2D control1,
        Point2D control2,
        Point2D startDirection,
        Point2D endDirection,
        IReadOnlyList<CubicSegment>? cubics = null)
    {
        Points = points;
        IsBezier = isBezier;
        Control1 = control1;
        Control2 = control2;
        StartDirection = startDirection;
        EndDirection = endDirection;
        Cubics = cubics;
    }

    /// <summary>Ordered points. For a bezier this is just [start, end].</summary>
    public IReadOnlyList<Point2D> Points { get; }

    public bool IsBezier { get; }

    /// <summary>First cubic control point (valid when <see cref="IsBezier"/>).</summary>
    public Point2D Control1 { get; }

    /// <summary>Second cubic control point (valid when <see cref="IsBezier"/>).</summary>
    public Point2D Control2 { get; }

    /// <summary>
    /// The ordered cubic segments of a smooth multi-segment curve, or null for polyline/single-bezier
    /// routes. When non-null the figure is <see cref="Start"/> followed by each segment's controls/end.
    /// </summary>
    public IReadOnlyList<CubicSegment>? Cubics { get; }

    public Point2D Start => Points[0];

    public Point2D End => Points[Points.Count - 1];

    /// <summary>Unit vector of the line leaving the source endpoint (never zero).</summary>
    public Point2D StartDirection { get; }

    /// <summary>Unit vector of the line entering the target endpoint (never zero).</summary>
    public Point2D EndDirection { get; }

    public static ConnectorRoute Polyline(IReadOnlyList<Point2D> points)
    {
        if (points is null || points.Count == 0)
        {
            throw new ArgumentException("A polyline route requires at least one point.", nameof(points));
        }

        if (points.Count == 1)
        {
            points = new[] { points[0], points[0] };
        }

        Point2D fallback = DefaultDirection(points[points.Count - 1] - points[0]);
        Point2D startDir = NonZeroDirectionOr(points[1] - points[0], fallback);
        Point2D endDir = NonZeroDirectionOr(points[points.Count - 1] - points[points.Count - 2], fallback);
        return new ConnectorRoute(points, isBezier: false, default, default, startDir, endDir);
    }

    public static ConnectorRoute Bezier(Point2D start, Point2D control1, Point2D control2, Point2D end)
    {
        Point2D fallback = DefaultDirection(end - start);
        Point2D startDir = NonZeroDirectionOr(control1 - start, fallback);
        Point2D endDir = NonZeroDirectionOr(end - control2, fallback);
        return new ConnectorRoute(new[] { start, end }, isBezier: true, control1, control2, startDir, endDir);
    }

    /// <summary>A smooth curve made of one or more cubic bezier segments starting at <paramref name="start"/>.</summary>
    public static ConnectorRoute PolyCubic(Point2D start, IReadOnlyList<CubicSegment> segments)
    {
        if (segments is null || segments.Count == 0)
        {
            throw new ArgumentException("A poly-cubic route requires at least one segment.", nameof(segments));
        }

        List<Point2D> points = new(segments.Count + 1) { start };
        foreach (CubicSegment segment in segments)
        {
            points.Add(segment.End);
        }

        Point2D end = points[points.Count - 1];
        Point2D fallback = DefaultDirection(end - start);
        Point2D startDir = NonZeroDirectionOr(segments[0].Control1 - start, fallback);
        Point2D endDir = NonZeroDirectionOr(end - segments[segments.Count - 1].Control2, fallback);
        return new ConnectorRoute(points, isBezier: false, default, default, startDir, endDir, segments);
    }

    private static Point2D DefaultDirection(Point2D delta)
    {
        Point2D normalized = delta.Normalized();
        return normalized.Length < 0.5d ? new Point2D(1, 0) : normalized;
    }

    private static Point2D NonZeroDirectionOr(Point2D delta, Point2D fallback)
    {
        Point2D normalized = delta.Normalized();
        return normalized.Length < 0.5d ? fallback : normalized;
    }
}

/// <summary>One cubic bezier segment of a <see cref="ConnectorRoute"/> curve (its start is the previous segment's end, or the route start for the first).</summary>
public readonly record struct CubicSegment(Point2D Control1, Point2D Control2, Point2D End);
