using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Jcl.Draw.Diagramming.Geometry;
using Jcl.Draw.Model.Nodes;
using ModelPoint = Jcl.Draw.Model.Primitives.Point2D;
using ModelRect = Jcl.Draw.Model.Primitives.Rect2D;

namespace Jcl.Draw.App.Rendering;

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
            _ => Polygon(ShapeOutline.GetPolygon(kind, new ModelRect(0, 0, width, height))),
        };
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
