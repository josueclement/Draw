using System.Collections.Generic;
using Jcl.Draw.Model.Primitives;

namespace Jcl.Draw.Diagramming.Routing;

/// <summary>
/// The computed geometry of a connector: either a polyline (<see cref="Points"/>) or a single
/// cubic bezier. Endpoint direction unit vectors are provided for orienting arrow/diamond/
/// triangle decorations.
/// </summary>
public sealed class ConnectorRoute
{
    private ConnectorRoute(
        IReadOnlyList<Point2D> points,
        bool isBezier,
        Point2D control1,
        Point2D control2,
        Point2D startDirection,
        Point2D endDirection)
    {
        Points = points;
        IsBezier = isBezier;
        Control1 = control1;
        Control2 = control2;
        StartDirection = startDirection;
        EndDirection = endDirection;
    }

    /// <summary>Ordered points. For a bezier this is just [start, end].</summary>
    public IReadOnlyList<Point2D> Points { get; }

    public bool IsBezier { get; }

    /// <summary>First cubic control point (valid when <see cref="IsBezier"/>).</summary>
    public Point2D Control1 { get; }

    /// <summary>Second cubic control point (valid when <see cref="IsBezier"/>).</summary>
    public Point2D Control2 { get; }

    public Point2D Start => Points[0];

    public Point2D End => Points[Points.Count - 1];

    /// <summary>Unit vector of the line leaving the source endpoint.</summary>
    public Point2D StartDirection { get; }

    /// <summary>Unit vector of the line entering the target endpoint.</summary>
    public Point2D EndDirection { get; }

    public static ConnectorRoute Polyline(IReadOnlyList<Point2D> points)
    {
        Point2D startDir = (points[1] - points[0]).Normalized();
        Point2D endDir = (points[points.Count - 1] - points[points.Count - 2]).Normalized();
        return new ConnectorRoute(points, isBezier: false, default, default, startDir, endDir);
    }

    public static ConnectorRoute Bezier(Point2D start, Point2D control1, Point2D control2, Point2D end)
    {
        Point2D startDir = (control1 - start).Normalized();
        Point2D endDir = (end - control2).Normalized();
        return new ConnectorRoute(new[] { start, end }, isBezier: true, control1, control2, startDir, endDir);
    }
}
