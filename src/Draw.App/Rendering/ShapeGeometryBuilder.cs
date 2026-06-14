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
            ShapeKind.RoundedRectangle => RoundedRectangle(width, height, cornerRadius),
            ShapeKind.Ellipse => new EllipseGeometry(new Rect(0, 0, width, height)),
            ShapeKind.Circle => Circle(width, height),
            ShapeKind.Note => NoteGeometry(width, height),
            ShapeKind.Cloud => CloudGeometry(width, height),
            _ => Polygon(ShapeOutline.GetPolygon(kind, new ModelRect(0, 0, width, height))),
        };
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
