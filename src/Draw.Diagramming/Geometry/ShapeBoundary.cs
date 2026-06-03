using System;
using System.Collections.Generic;
using Draw.Model.Nodes;
using Draw.Model.Primitives;

namespace Draw.Diagramming.Geometry;

/// <summary>Computes where a ray leaving a shape's center crosses its outline.</summary>
public static class ShapeBoundary
{
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
            if (TryIntersectSegment(origin, direction, a, b, out double t, out Point2D hit) && t > 1e-9 && t < bestT)
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
        if (Math.Abs(denominator) < 1e-12)
        {
            return false; // parallel
        }

        Point2D diff = a - origin;
        t = Cross(diff, edge) / denominator;
        double s = Cross(diff, direction) / denominator;
        if (t < 0 || s < -1e-9 || s > 1 + 1e-9)
        {
            return false;
        }

        hit = new Point2D(origin.X + (t * direction.X), origin.Y + (t * direction.Y));
        return true;
    }

    private static double Cross(Point2D u, Point2D v) => (u.X * v.Y) - (u.Y * v.X);

    private static double PositiveMin(double a, double b)
    {
        bool aPos = a > 1e-9;
        bool bPos = b > 1e-9;
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
