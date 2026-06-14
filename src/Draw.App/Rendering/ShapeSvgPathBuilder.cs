using System;
using System.Collections.Generic;
using System.Text;
using Draw.Diagramming.Geometry;
using Draw.Model.Nodes;
using static Draw.App.Rendering.SvgFormat;
using ModelPoint = Draw.Model.Primitives.Point2D;
using ModelRect = Draw.Model.Primitives.Rect2D;

namespace Draw.App.Rendering;

/// <summary>An SVG outline for a shape: the fillable body path and, for notes, an extra stroke-only path.</summary>
public readonly record struct SvgShape(string FillPath, string? StrokeOnlyPath);

/// <summary>
/// Builds the SVG <c>path</c> data for each <see cref="ShapeKind"/> at a given size, mirroring
/// <see cref="ShapeGeometryBuilder"/> (and reusing <see cref="ShapeOutline"/> for polygons) so the SVG
/// export matches the on-screen rendering. Coordinates are local (origin at the shape's top-left); the
/// caller positions the shape with a <c>translate</c>.
/// </summary>
public static class ShapeSvgPathBuilder
{
    public static SvgShape Build(ShapeKind kind, double width, double height, double cornerRadius)
    {
        width = Math.Max(0d, width);
        height = Math.Max(0d, height);

        return kind switch
        {
            ShapeKind.Rectangle => new SvgShape(Rectangle(width, height), null),
            ShapeKind.RoundedRectangle => new SvgShape(RoundedRectangle(width, height, cornerRadius), null),
            ShapeKind.Ellipse => new SvgShape(Ellipse(0d, 0d, width, height), null),
            ShapeKind.Circle => new SvgShape(Circle(width, height), null),
            ShapeKind.Note => Note(width, height),
            ShapeKind.Cloud => new SvgShape(Cloud(width, height), null),
            ShapeKind.Terminator => new SvgShape(RoundedRectangle(width, height, Math.Min(width, height) / 2d), null),
            ShapeKind.Cylinder => Cylinder(width, height),
            ShapeKind.Document => new SvgShape(Document(width, height), null),
            ShapeKind.PredefinedProcess => PredefinedProcess(width, height),
            ShapeKind.Display => new SvgShape(Display(width, height), null),
            ShapeKind.Delay => new SvgShape(Delay(width, height), null),
            _ => new SvgShape(Polygon(ShapeOutline.GetPolygon(kind, new ModelRect(0d, 0d, width, height))), null),
        };
    }

    // SVG sweep flag 1 == SweepDirection.Clockwise, 0 == CounterClockwise (mirrors ShapeGeometryBuilder).
    private static SvgShape Cylinder(double w, double h)
    {
        double ry = Math.Min(h * 0.18d, h / 2d);
        double rx = w / 2d;

        string body =
            $"M0,{Num(ry)} A{Num(rx)},{Num(ry)} 0 0 1 {Num(w)},{Num(ry)} " +
            $"L{Num(w)},{Num(h - ry)} A{Num(rx)},{Num(ry)} 0 0 1 0,{Num(h - ry)} Z";
        string frontRim = $"M0,{Num(ry)} A{Num(rx)},{Num(ry)} 0 0 0 {Num(w)},{Num(ry)}";
        return new SvgShape(body, frontRim);
    }

    private static string Document(double w, double h)
    {
        double d = Math.Min(h * 0.14d, h / 2d);
        return $"M0,0 L{Num(w)},0 L{Num(w)},{Num(h - d)} " +
               $"C{Num(w * 0.66d)},{Num(h)} {Num(w * 0.33d)},{Num(h - (2d * d))} 0,{Num(h - d)} Z";
    }

    private static SvgShape PredefinedProcess(double w, double h)
    {
        double bar = Math.Min(w * 0.12d, w / 2d);
        string bars = $"M{Num(bar)},0 L{Num(bar)},{Num(h)} M{Num(w - bar)},0 L{Num(w - bar)},{Num(h)}";
        return new SvgShape(Rectangle(w, h), bars);
    }

    private static string Display(double w, double h)
    {
        double rx = w * 0.2d;
        double ry = h / 2d;
        return $"M{Num(rx)},0 L{Num(w * 0.8d)},0 A{Num(rx)},{Num(ry)} 0 0 1 {Num(w * 0.8d)},{Num(h)} " +
               $"L{Num(rx)},{Num(h)} Q0,{Num(ry)} {Num(rx)},0 Z";
    }

    private static string Delay(double w, double h)
    {
        double rx = Math.Min(h / 2d, w);
        double ry = h / 2d;
        return $"M0,0 L{Num(w - rx)},0 A{Num(rx)},{Num(ry)} 0 0 1 {Num(w - rx)},{Num(h)} L0,{Num(h)} Z";
    }

    private static string Cloud(double w, double h)
    {
        (ModelPoint start, IReadOnlyList<ShapeOutline.QuadSegment> segments) =
            ShapeOutline.CloudCurve(new ModelRect(0d, 0d, w, h));

        StringBuilder sb = new();
        sb.Append("M").Append(Num(start.X)).Append(',').Append(Num(start.Y));
        foreach (ShapeOutline.QuadSegment segment in segments)
        {
            sb.Append(" Q").Append(Num(segment.Control.X)).Append(',').Append(Num(segment.Control.Y))
              .Append(' ').Append(Num(segment.End.X)).Append(',').Append(Num(segment.End.Y));
        }

        sb.Append(" Z");
        return sb.ToString();
    }

    private static string Rectangle(double w, double h)
        => $"M0,0 H{Num(w)} V{Num(h)} H0 Z";

    private static string RoundedRectangle(double w, double h, double cornerRadius)
    {
        double r = Math.Max(0d, Math.Min(cornerRadius, Math.Min(w, h) / 2d));
        if (r <= 0d)
        {
            return Rectangle(w, h);
        }

        // Clockwise, matching ShapeGeometryBuilder (SweepDirection.Clockwise => SVG sweep flag 1).
        return $"M{Num(r)},0 L{Num(w - r)},0 A{Num(r)},{Num(r)} 0 0 1 {Num(w)},{Num(r)} " +
               $"L{Num(w)},{Num(h - r)} A{Num(r)},{Num(r)} 0 0 1 {Num(w - r)},{Num(h)} " +
               $"L{Num(r)},{Num(h)} A{Num(r)},{Num(r)} 0 0 1 0,{Num(h - r)} " +
               $"L0,{Num(r)} A{Num(r)},{Num(r)} 0 0 1 {Num(r)},0 Z";
    }

    private static string Ellipse(double x, double y, double w, double h)
    {
        double rx = w / 2d;
        double ry = h / 2d;
        double cy = y + ry;
        return $"M{Num(x)},{Num(cy)} A{Num(rx)},{Num(ry)} 0 1 0 {Num(x + w)},{Num(cy)} " +
               $"A{Num(rx)},{Num(ry)} 0 1 0 {Num(x)},{Num(cy)} Z";
    }

    private static string Circle(double w, double h)
    {
        double diameter = Math.Min(w, h);
        double x = (w - diameter) / 2d;
        double y = (h - diameter) / 2d;
        return Ellipse(x, y, diameter, diameter);
    }

    private static SvgShape Note(double w, double h)
    {
        double fold = Math.Clamp(Math.Min(w, h) * 0.22d, 6d, 18d);
        fold = Math.Min(fold, Math.Min(w, h));

        string body = $"M0,0 L{Num(w - fold)},0 L{Num(w)},{Num(fold)} L{Num(w)},{Num(h)} L0,{Num(h)} Z";
        string foldEdge = $"M{Num(w - fold)},0 L{Num(w - fold)},{Num(fold)} L{Num(w)},{Num(fold)}";
        return new SvgShape(body, foldEdge);
    }

    private static string Polygon(IReadOnlyList<ModelPoint> points)
    {
        StringBuilder sb = new();
        sb.Append("M").Append(Num(points[0].X)).Append(',').Append(Num(points[0].Y));
        for (int i = 1; i < points.Count; i++)
        {
            sb.Append(" L").Append(Num(points[i].X)).Append(',').Append(Num(points[i].Y));
        }

        sb.Append(" Z");
        return sb.ToString();
    }
}
