using System;
using Avalonia;
using Avalonia.Media;
using Jcl.Draw.Model.Nodes;

namespace Jcl.Draw.App.Rendering;

/// <summary>Builds the outline <see cref="Geometry"/> for each <see cref="ShapeKind"/> at a given size.</summary>
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
            ShapeKind.Diamond => Polygon((width / 2, 0), (width, height / 2), (width / 2, height), (0, height / 2)),
            ShapeKind.Parallelogram => Parallelogram(width, height),
            ShapeKind.Trapezoid => Trapezoid(width, height),
            ShapeKind.Triangle => Polygon((width / 2, 0), (width, height), (0, height)),
            _ => new RectangleGeometry(new Rect(0, 0, width, height)),
        };
    }

    private static Geometry Circle(double width, double height)
    {
        double diameter = Math.Min(width, height);
        double x = (width - diameter) / 2;
        double y = (height - diameter) / 2;
        return new EllipseGeometry(new Rect(x, y, diameter, diameter));
    }

    private static Geometry Parallelogram(double width, double height)
    {
        double offset = Math.Min(width * 0.25, width / 2);
        return Polygon((offset, 0), (width, 0), (width - offset, height), (0, height));
    }

    private static Geometry Trapezoid(double width, double height)
    {
        double offset = Math.Min(width * 0.2, width / 2);
        return Polygon((offset, 0), (width - offset, 0), (width, height), (0, height));
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

    private static Geometry Polygon(params (double X, double Y)[] points)
    {
        StreamGeometry geometry = new();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(points[0].X, points[0].Y), isFilled: true);
            for (int i = 1; i < points.Length; i++)
            {
                ctx.LineTo(new Point(points[i].X, points[i].Y));
            }

            ctx.EndFigure(true);
        }

        return geometry;
    }
}
