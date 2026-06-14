using System;
using System.Collections.Generic;
using Draw.Model.Nodes;
using Draw.Model.Primitives;

namespace Draw.Diagramming.Geometry;

/// <summary>
/// Canonical, framework-agnostic outline of each shape kind. Used both for boundary
/// intersection (connector attachment) and as the source of truth for rendering geometry.
/// </summary>
public static class ShapeOutline
{
    public static bool IsElliptical(ShapeKind kind) => kind is ShapeKind.Ellipse or ShapeKind.Circle;

    /// <summary>The bounding rectangle of the ellipse drawn for an elliptical shape.</summary>
    public static Rect2D EllipseBounds(ShapeKind kind, Rect2D bounds)
    {
        if (kind == ShapeKind.Circle)
        {
            double diameter = Math.Min(bounds.Width, bounds.Height);
            double x = bounds.X + ((bounds.Width - diameter) / 2);
            double y = bounds.Y + ((bounds.Height - diameter) / 2);
            return new Rect2D(x, y, diameter, diameter);
        }

        return bounds;
    }

    /// <summary>
    /// Closed polygon vertices (world coordinates) for polygonal shapes. Rounded rectangles
    /// use the plain rectangle (corner rounding is negligible for attachment). Returns an
    /// empty list for elliptical shapes; use <see cref="EllipseBounds"/> for those.
    /// </summary>
    public static IReadOnlyList<Point2D> GetPolygon(ShapeKind kind, Rect2D bounds)
    {
        double x = bounds.X;
        double y = bounds.Y;
        double w = bounds.Width;
        double h = bounds.Height;

        Point2D P(double lx, double ly) => new(x + lx, y + ly);

        switch (kind)
        {
            case ShapeKind.Rectangle:
            case ShapeKind.RoundedRectangle:
            // Connectors attach to the full rectangle; the small dog-ear is ignored for routing.
            case ShapeKind.Note:
            // Curved flowchart shapes attach to their bounding rectangle (a good approximation: each
            // touches all four edges at its mid-points). Their lobed/capped silhouettes are drawn by
            // the geometry builders.
            case ShapeKind.Terminator:
            case ShapeKind.Cylinder:
            case ShapeKind.Document:
            case ShapeKind.PredefinedProcess:
            case ShapeKind.Display:
            case ShapeKind.Delay:
                return new[] { P(0, 0), P(w, 0), P(w, h), P(0, h) };

            // Manual input: a quadrilateral whose top edge slopes up from left to right.
            case ShapeKind.ManualInput:
                return new[] { P(0, h * 0.25), P(w, 0), P(w, h), P(0, h) };

            // Off-page connector: a rectangle tapering to a point at the bottom centre.
            case ShapeKind.OffPageConnector:
                return new[] { P(0, 0), P(w, 0), P(w, h * 0.6), P(w / 2, h), P(0, h * 0.6) };

            // Block arrows: a shaft (middle third) with a triangular head spanning the full extent.
            case ShapeKind.ArrowRight:
                return new[]
                {
                    P(0, h / 3), P(w * 0.6, h / 3), P(w * 0.6, 0), P(w, h / 2),
                    P(w * 0.6, h), P(w * 0.6, 2 * h / 3), P(0, 2 * h / 3),
                };

            case ShapeKind.ArrowLeft:
                return new[]
                {
                    P(w, h / 3), P(w, 2 * h / 3), P(w * 0.4, 2 * h / 3), P(w * 0.4, h),
                    P(0, h / 2), P(w * 0.4, 0), P(w * 0.4, h / 3),
                };

            case ShapeKind.ArrowUp:
                return new[]
                {
                    P(w / 3, h), P(w / 3, h * 0.4), P(0, h * 0.4), P(w / 2, 0),
                    P(w, h * 0.4), P(2 * w / 3, h * 0.4), P(2 * w / 3, h),
                };

            case ShapeKind.ArrowDown:
                return new[]
                {
                    P(w / 3, 0), P(2 * w / 3, 0), P(2 * w / 3, h * 0.6), P(w, h * 0.6),
                    P(w / 2, h), P(0, h * 0.6), P(w / 3, h * 0.6),
                };

            case ShapeKind.ArrowDouble:
                return new[]
                {
                    P(0, h / 2), P(w * 0.25, 0), P(w * 0.25, h / 3), P(w * 0.75, h / 3), P(w * 0.75, 0),
                    P(w, h / 2), P(w * 0.75, h), P(w * 0.75, 2 * h / 3), P(w * 0.25, 2 * h / 3), P(w * 0.25, h),
                };

            case ShapeKind.Diamond:
                return new[] { P(w / 2, 0), P(w, h / 2), P(w / 2, h), P(0, h / 2) };

            case ShapeKind.Parallelogram:
            {
                double off = Math.Min(w * 0.25, w / 2);
                return new[] { P(off, 0), P(w, 0), P(w - off, h), P(0, h) };
            }

            case ShapeKind.Trapezoid:
            {
                double off = Math.Min(w * 0.2, w / 2);
                return new[] { P(off, 0), P(w - off, 0), P(w, h), P(0, h) };
            }

            case ShapeKind.Triangle:
                return new[] { P(w / 2, 0), P(w, h), P(0, h) };

            // Horizontal hexagon (points at the left and right); the "preparation" flowchart silhouette.
            case ShapeKind.Hexagon:
                return RegularPolygon(6, x, y, w, h, 0d);

            // Upward-pointing regular pentagon (apex at top centre).
            case ShapeKind.Pentagon:
                return RegularPolygon(5, x, y, w, h, -90d);

            // Flat-top regular octagon (stop-sign orientation).
            case ShapeKind.Octagon:
                return RegularPolygon(8, x, y, w, h, 22.5d);

            case ShapeKind.Star:
                return Star(x, y, w, h);

            case ShapeKind.Cross:
            {
                double tw = w / 3d;
                double th = h / 3d;
                return new[]
                {
                    P(tw, 0), P(2d * tw, 0), P(2d * tw, th), P(w, th), P(w, 2d * th), P(2d * tw, 2d * th),
                    P(2d * tw, h), P(tw, h), P(tw, 2d * th), P(0, 2d * th), P(0, th), P(tw, th),
                };
            }

            // A speech callout: rectangular body (to 72% height) with a tail at the lower-left.
            case ShapeKind.Callout:
            {
                double b = h * 0.72d;
                return new[]
                {
                    P(0, 0), P(w, 0), P(w, b), P(w * 0.40d, b), P(w * 0.18d, h), P(w * 0.25d, b), P(0, b),
                };
            }

            // The cloud's lobed outline is drawn by the geometry builders; connectors attach to the
            // bounding rectangle (an acceptable approximation, like Note's fold).
            case ShapeKind.Cloud:
                return new[] { P(0, 0), P(w, 0), P(w, h), P(0, h) };

            default:
                return Array.Empty<Point2D>();
        }
    }

    /// <summary>A single quadratic-Bézier segment: an off-curve <see cref="Control"/> point and the on-curve <see cref="End"/>.</summary>
    public readonly record struct QuadSegment(Point2D Control, Point2D End);

    /// <summary>
    /// The cloud outline as a closed run of quadratic-Bézier lobes (a scalloped ellipse). Shared by the
    /// Avalonia geometry and SVG path builders so screen and export render identically.
    /// </summary>
    public static (Point2D Start, IReadOnlyList<QuadSegment> Segments) CloudCurve(Rect2D bounds)
    {
        const int lobes = 6;
        const double anchorRatio = 0.42d;
        const double controlRatio = 0.74d;

        double cx = bounds.X + (bounds.Width / 2d);
        double cy = bounds.Y + (bounds.Height / 2d);
        double rx = bounds.Width / 2d;
        double ry = bounds.Height / 2d;

        Point2D Anchor(int i)
        {
            double a = (-90d + (i * (360d / lobes))) * Math.PI / 180d;
            return new Point2D(cx + (rx * anchorRatio * Math.Cos(a)), cy + (ry * anchorRatio * Math.Sin(a)));
        }

        Point2D Control(int i)
        {
            double a = (-90d + ((i + 0.5d) * (360d / lobes))) * Math.PI / 180d;
            return new Point2D(cx + (rx * controlRatio * Math.Cos(a)), cy + (ry * controlRatio * Math.Sin(a)));
        }

        QuadSegment[] segments = new QuadSegment[lobes];
        for (int i = 0; i < lobes; i++)
        {
            segments[i] = new QuadSegment(Control(i), Anchor((i + 1) % lobes));
        }

        return (Anchor(0), segments);
    }

    /// <summary>Vertices of a regular polygon inscribed in <paramref name="w"/>×<paramref name="h"/>, the first at <paramref name="startDeg"/> (degrees, clockwise, y-down).</summary>
    private static IReadOnlyList<Point2D> RegularPolygon(int sides, double x, double y, double w, double h, double startDeg)
    {
        double cx = x + (w / 2d);
        double cy = y + (h / 2d);
        double rx = w / 2d;
        double ry = h / 2d;

        Point2D[] points = new Point2D[sides];
        for (int i = 0; i < sides; i++)
        {
            double angle = (startDeg + (i * (360d / sides))) * Math.PI / 180d;
            points[i] = new Point2D(cx + (rx * Math.Cos(angle)), cy + (ry * Math.Sin(angle)));
        }

        return points;
    }

    /// <summary>A five-pointed star (pentagram), apex up, with the standard inner/outer radius ratio.</summary>
    private static IReadOnlyList<Point2D> Star(double x, double y, double w, double h)
    {
        const double innerRatio = 0.382d;
        double cx = x + (w / 2d);
        double cy = y + (h / 2d);
        double rx = w / 2d;
        double ry = h / 2d;

        Point2D[] points = new Point2D[10];
        for (int i = 0; i < 10; i++)
        {
            double radius = (i % 2 == 0) ? 1d : innerRatio;
            double angle = (-90d + (i * 36d)) * Math.PI / 180d;
            points[i] = new Point2D(cx + (rx * radius * Math.Cos(angle)), cy + (ry * radius * Math.Sin(angle)));
        }

        return points;
    }
}
