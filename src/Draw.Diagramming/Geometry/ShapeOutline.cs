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
                return new[] { P(0, 0), P(w, 0), P(w, h), P(0, h) };

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

            default:
                return Array.Empty<Point2D>();
        }
    }
}
