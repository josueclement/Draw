using System;
using System.Collections.Generic;
using Draw.Model.Nodes;
using Draw.Model.Primitives;

namespace Draw.Diagramming.Geometry;

/// <summary>Computes where a ray leaving a shape's center crosses its outline.</summary>
public static class ShapeBoundary
{
    // Geometry tolerances. These are intentionally distinct — they guard different quantities, so
    // collapsing them into one value would be wrong:
    //
    //  * ParameterEpsilon — slack on the dimensionless ray/segment parameters (t along the ray, s
    //    along an edge). A hit at t ~= 0 (back at the origin) is rejected, and a hit a hair past a
    //    vertex (s just outside [0,1]) is still accepted, so rounding never drops a real crossing.
    //  * ParallelEpsilon — threshold on a cross product (an area), below which a ray and an edge are
    //    treated as parallel. Cross products scale with their operands, so this sits well below
    //    pixel-scale geometry yet above the floating-point noise of the products that feed it.
    //
    // The zero-length / degenerate guards below deliberately use double.Epsilon (the smallest
    // positive double): they ask "is this exactly zero (or denormal)?", a different question from
    // "is this within tolerance?" — widening them would change which inputs fall back to the centre.
    private const double ParameterEpsilon = 1e-9;
    private const double ParallelEpsilon = 1e-12;

    /// <summary>
    /// The point on the shape outline where the ray from the shape's center toward
    /// <paramref name="toward"/> exits. Falls back to the center for degenerate input.
    /// </summary>
    public static Point2D IntersectFromCenter(ShapeKind kind, Rect2D bounds, Point2D toward)
    {
        Point2D center = bounds.Center;
        Point2D direction = toward - center;
        if (direction.Length <= double.Epsilon)
        {
            return center;
        }

        if (ShapeOutline.IsElliptical(kind))
        {
            return IntersectEllipse(ShapeOutline.EllipseBounds(kind, bounds), center, direction);
        }

        IReadOnlyList<Point2D> polygon = ShapeOutline.GetPolygon(kind, bounds);
        return IntersectPolygon(polygon, center, direction) ?? center;
    }

    /// <summary>
    /// Resolves a forced attachment expressed as a relative (u,v) point in [0,1]² of
    /// <paramref name="bounds"/> to a point on the shape outline. The relative point is cast as a
    /// ray from the shape centre, so the result lands on the outline (the shape kinds in use are
    /// convex, giving a single intersection). Falls back to the centre for degenerate bounds.
    /// </summary>
    public static Point2D ResolveAnchor(ShapeKind kind, Rect2D bounds, Point2D relative)
    {
        Point2D toward = new(
            bounds.X + (relative.X * bounds.Width),
            bounds.Y + (relative.Y * bounds.Height));
        return IntersectFromCenter(kind, bounds, toward);
    }

    private static Point2D IntersectEllipse(Rect2D ellipse, Point2D center, Point2D direction)
    {
        double rx = ellipse.Width / 2;
        double ry = ellipse.Height / 2;
        if (rx <= 0 || ry <= 0)
        {
            return center;
        }

        // Ellipse is centered at its own center, which may differ from the shape center for circles.
        Point2D ellipseCenter = ellipse.Center;
        double ox = center.X - ellipseCenter.X;
        double oy = center.Y - ellipseCenter.Y;

        // Solve ((ox + t*dx)/rx)^2 + ((oy + t*dy)/ry)^2 = 1 for the positive root.
        double dx = direction.X;
        double dy = direction.Y;
        double a = ((dx * dx) / (rx * rx)) + ((dy * dy) / (ry * ry));
        double b = 2 * (((ox * dx) / (rx * rx)) + ((oy * dy) / (ry * ry)));
        double c = ((ox * ox) / (rx * rx)) + ((oy * oy) / (ry * ry)) - 1;

        double discriminant = (b * b) - (4 * a * c);
        if (a <= double.Epsilon || discriminant < 0)
        {
            return center;
        }

        double sqrt = Math.Sqrt(discriminant);
        double t1 = (-b + sqrt) / (2 * a);
        double t2 = (-b - sqrt) / (2 * a);
        double t = PositiveMin(t1, t2);
        if (double.IsNaN(t))
        {
            return center;
        }

        return new Point2D(center.X + (t * dx), center.Y + (t * dy));
    }

    private static Point2D? IntersectPolygon(IReadOnlyList<Point2D> polygon, Point2D origin, Point2D direction)
    {
        if (polygon.Count < 2)
        {
            return null;
        }

        double bestT = double.PositiveInfinity;
        Point2D? best = null;

        for (int i = 0; i < polygon.Count; i++)
        {
            Point2D a = polygon[i];
            Point2D b = polygon[(i + 1) % polygon.Count];
            if (TryIntersectSegment(origin, direction, a, b, out double t, out Point2D hit) && t > ParameterEpsilon && t < bestT)
            {
                bestT = t;
                best = hit;
            }
        }

        return best;
    }

    // Ray origin + t*direction (t >= 0) against segment a..b (s in [0,1]).
    private static bool TryIntersectSegment(Point2D origin, Point2D direction, Point2D a, Point2D b, out double t, out Point2D hit)
    {
        t = 0;
        hit = default;

        Point2D edge = b - a;
        double denominator = Cross(direction, edge);
        if (Math.Abs(denominator) < ParallelEpsilon)
        {
            return false; // parallel
        }

        Point2D diff = a - origin;
        t = Cross(diff, edge) / denominator;
        double s = Cross(diff, direction) / denominator;
        if (t < 0 || s < -ParameterEpsilon || s > 1 + ParameterEpsilon)
        {
            return false;
        }

        hit = new Point2D(origin.X + (t * direction.X), origin.Y + (t * direction.Y));
        return true;
    }

    private static double Cross(Point2D u, Point2D v) => (u.X * v.Y) - (u.Y * v.X);

    private static double PositiveMin(double a, double b)
    {
        bool aPos = a > ParameterEpsilon;
        bool bPos = b > ParameterEpsilon;
        if (aPos && bPos)
        {
            return Math.Min(a, b);
        }

        if (aPos)
        {
            return a;
        }

        return bPos ? b : double.NaN;
    }
}
