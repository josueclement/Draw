using System;
using Avalonia;
using Avalonia.Media;

namespace Jcl.Draw.App.Rendering;

/// <summary>Builds the stick-figure outline for an actor node, scaled to its bounds and
/// reserving a bottom strip for the name label.</summary>
public static class ActorGeometry
{
    public static Geometry Build(double width, double height)
    {
        width = Math.Max(1d, width);
        height = Math.Max(1d, height);

        double labelStrip = Math.Min(18d, height * 0.25);
        double figureHeight = Math.Max(1d, height - labelStrip);
        double cx = width / 2d;
        double headRadius = Math.Min(width, figureHeight) * 0.18d;
        double neckY = headRadius * 2d;
        double hipY = figureHeight * 0.62d;
        double shoulderY = neckY + ((hipY - neckY) * 0.25d);
        double armHalf = width * 0.30d;
        double legSpread = width * 0.28d;

        GeometryGroup group = new();
        group.Children.Add(new EllipseGeometry(new Rect(cx - headRadius, 0, headRadius * 2d, headRadius * 2d)));

        StreamGeometry lines = new();
        using (StreamGeometryContext ctx = lines.Open())
        {
            ctx.BeginFigure(new Point(cx, neckY), isFilled: false);
            ctx.LineTo(new Point(cx, hipY));
            ctx.EndFigure(false);

            ctx.BeginFigure(new Point(cx - armHalf, shoulderY), isFilled: false);
            ctx.LineTo(new Point(cx + armHalf, shoulderY));
            ctx.EndFigure(false);

            ctx.BeginFigure(new Point(cx - legSpread, figureHeight), isFilled: false);
            ctx.LineTo(new Point(cx, hipY));
            ctx.LineTo(new Point(cx + legSpread, figureHeight));
            ctx.EndFigure(false);
        }

        group.Children.Add(lines);
        return group;
    }
}
