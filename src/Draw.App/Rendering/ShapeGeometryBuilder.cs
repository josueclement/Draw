using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Draw.Diagramming.Geometry;
using Draw.Model.Nodes;
using ModelPoint = Draw.Model.Primitives.Point2D;
using ModelRect = Draw.Model.Primitives.Rect2D;

namespace Draw.App.Rendering;

/// <summary>
/// Builds the outline <see cref="Geometry"/> for each <see cref="ShapeKind"/> at a given size,
/// reusing <see cref="ShapeOutline"/> so rendering and connector routing agree.
/// </summary>
public static class ShapeGeometryBuilder
{
    public static Geometry Build(ShapeKind kind, double width, double height, double cornerRadius)
    {
        width = Math.Max(0d, width);
        height = Math.Max(0d, height);

        return kind switch
        {
            ShapeKind.Rectangle => new RectangleGeometry(new Rect(0, 0, width, height)),
            ShapeKind.MindMapTopic => new RectangleGeometry(new Rect(0, 0, width, height)),
            ShapeKind.RoundedRectangle => RoundedRectangle(width, height, cornerRadius),
            ShapeKind.MindMapTopicRounded => RoundedRectangle(width, height, cornerRadius),
            ShapeKind.Ellipse => new EllipseGeometry(new Rect(0, 0, width, height)),
            ShapeKind.Circle => Circle(width, height),
            ShapeKind.Note => NoteGeometry(width, height),
            ShapeKind.Cloud => CloudGeometry(width, height),
            // A stadium is a rounded rectangle whose corner radius spans the short side.
            ShapeKind.Terminator => RoundedRectangle(width, height, Math.Min(width, height) / 2d),
            ShapeKind.Cylinder => CylinderGeometry(width, height),
            ShapeKind.Document => DocumentGeometry(width, height),
            ShapeKind.PredefinedProcess => PredefinedProcessGeometry(width, height),
            ShapeKind.Display => DisplayGeometry(width, height),
            ShapeKind.Delay => DelayGeometry(width, height),
            _ => Polygon(ShapeOutline.GetPolygon(kind, new ModelRect(0, 0, width, height))),
        };
    }

    private static Geometry CylinderGeometry(double width, double height)
    {
        double ry = Math.Min(height * 0.18d, height / 2d);
        Size cap = new(width / 2d, ry);

        StreamGeometry geometry = new();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            // Body silhouette: back rim (top, bulging up) + sides + front bottom arc (bulging down).
            ctx.BeginFigure(new Point(0, ry), isFilled: true);
            ctx.ArcTo(new Point(width, ry), cap, 0, false, SweepDirection.Clockwise);
            ctx.LineTo(new Point(width, height - ry));
            ctx.ArcTo(new Point(0, height - ry), cap, 0, false, SweepDirection.Clockwise);
            ctx.EndFigure(true);

            // Front rim of the top lid (bulging down) — stroke only, completes the visible ellipse.
            ctx.BeginFigure(new Point(0, ry), isFilled: false);
            ctx.ArcTo(new Point(width, ry), cap, 0, false, SweepDirection.CounterClockwise);
            ctx.EndFigure(false);
        }

        return geometry;
    }

    private static Geometry DocumentGeometry(double width, double height)
    {
        double d = Math.Min(height * 0.14d, height / 2d);

        StreamGeometry geometry = new();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(0, 0), isFilled: true);
            ctx.LineTo(new Point(width, 0));
            ctx.LineTo(new Point(width, height - d));
            // Wavy bottom edge, right to left: dip then rise (an S-curve).
            ctx.CubicBezierTo(
                new Point(width * 0.66d, height),
                new Point(width * 0.33d, height - (2d * d)),
                new Point(0, height - d));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry PredefinedProcessGeometry(double width, double height)
    {
        double bar = Math.Min(width * 0.12d, width / 2d);

        StreamGeometry geometry = new();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(0, 0), isFilled: true);
            ctx.LineTo(new Point(width, 0));
            ctx.LineTo(new Point(width, height));
            ctx.LineTo(new Point(0, height));
            ctx.EndFigure(true);

            // The two inner bars — stroke only.
            ctx.BeginFigure(new Point(bar, 0), isFilled: false);
            ctx.LineTo(new Point(bar, height));
            ctx.EndFigure(false);

            ctx.BeginFigure(new Point(width - bar, 0), isFilled: false);
            ctx.LineTo(new Point(width - bar, height));
            ctx.EndFigure(false);
        }

        return geometry;
    }

    private static Geometry DisplayGeometry(double width, double height)
    {
        Size cap = new(width * 0.2d, height / 2d);

        StreamGeometry geometry = new();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(width * 0.2d, 0), isFilled: true);
            ctx.LineTo(new Point(width * 0.8d, 0));
            ctx.ArcTo(new Point(width * 0.8d, height), cap, 0, false, SweepDirection.Clockwise);
            ctx.LineTo(new Point(width * 0.2d, height));
            ctx.QuadraticBezierTo(new Point(0, height / 2d), new Point(width * 0.2d, 0));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry DelayGeometry(double width, double height)
    {
        double rx = Math.Min(height / 2d, width);
        Size cap = new(rx, height / 2d);

        StreamGeometry geometry = new();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(0, 0), isFilled: true);
            ctx.LineTo(new Point(width - rx, 0));
            ctx.ArcTo(new Point(width - rx, height), cap, 0, false, SweepDirection.Clockwise);
            ctx.LineTo(new Point(0, height));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry CloudGeometry(double width, double height)
    {
        (ModelPoint start, IReadOnlyList<ShapeOutline.QuadSegment> segments) =
            ShapeOutline.CloudCurve(new ModelRect(0, 0, width, height));

        StreamGeometry geometry = new();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(start.X, start.Y), isFilled: true);
            foreach (ShapeOutline.QuadSegment segment in segments)
            {
                ctx.QuadraticBezierTo(new Point(segment.Control.X, segment.Control.Y), new Point(segment.End.X, segment.End.Y));
            }

            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry NoteGeometry(double width, double height)
    {
        // Rectangle with a folded top-right corner. The body figure renders the outline (incl. the
        // diagonal fold edge); a second open figure strokes the fold's underside. Deliberately
        // differs from ShapeOutline.Note (a plain rectangle) used for connector routing.
        double fold = Math.Clamp(Math.Min(width, height) * 0.22, 6d, 18d);
        fold = Math.Min(fold, Math.Min(width, height));

        StreamGeometry geometry = new();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(0, 0), isFilled: true);
            ctx.LineTo(new Point(width - fold, 0));
            ctx.LineTo(new Point(width, fold));
            ctx.LineTo(new Point(width, height));
            ctx.LineTo(new Point(0, height));
            ctx.EndFigure(true);

            ctx.BeginFigure(new Point(width - fold, 0), isFilled: false);
            ctx.LineTo(new Point(width - fold, fold));
            ctx.LineTo(new Point(width, fold));
            ctx.EndFigure(false);
        }

        return geometry;
    }

    private static Geometry Circle(double width, double height)
    {
        double diameter = Math.Min(width, height);
        double x = (width - diameter) / 2;
        double y = (height - diameter) / 2;
        return new EllipseGeometry(new Rect(x, y, diameter, diameter));
    }

    private static Geometry RoundedRectangle(double width, double height, double cornerRadius)
    {
        double r = Math.Max(0d, Math.Min(cornerRadius, Math.Min(width, height) / 2));
        if (r <= 0d)
        {
            return new RectangleGeometry(new Rect(0, 0, width, height));
        }

        Size corner = new(r, r);
        StreamGeometry geometry = new();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(r, 0), isFilled: true);
            ctx.LineTo(new Point(width - r, 0));
            ctx.ArcTo(new Point(width, r), corner, 0, false, SweepDirection.Clockwise);
            ctx.LineTo(new Point(width, height - r));
            ctx.ArcTo(new Point(width - r, height), corner, 0, false, SweepDirection.Clockwise);
            ctx.LineTo(new Point(r, height));
            ctx.ArcTo(new Point(0, height - r), corner, 0, false, SweepDirection.Clockwise);
            ctx.LineTo(new Point(0, r));
            ctx.ArcTo(new Point(r, 0), corner, 0, false, SweepDirection.Clockwise);
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry Polygon(IReadOnlyList<ModelPoint> points)
    {
        StreamGeometry geometry = new();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(points[0].X, points[0].Y), isFilled: true);
            for (int i = 1; i < points.Count; i++)
            {
                ctx.LineTo(new Point(points[i].X, points[i].Y));
            }

            ctx.EndFigure(true);
        }

        return geometry;
    }
}
