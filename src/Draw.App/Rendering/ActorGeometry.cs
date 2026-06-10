using Avalonia;
using Avalonia.Media;
using Draw.Diagramming.Geometry;

namespace Draw.App.Rendering;

/// <summary>Builds the stick-figure outline for an actor node from the shared
/// <see cref="ActorDimensions"/> proportions, as Avalonia geometry.</summary>
public static class ActorGeometry
{
    public static Geometry Build(double width, double height)
    {
        ActorDimensions d = new(width, height);

        GeometryGroup group = new();
        group.Children.Add(new EllipseGeometry(new Rect(d.CenterX - d.HeadRadius, 0, d.HeadRadius * 2d, d.HeadRadius * 2d)));

        StreamGeometry lines = new();
        using (StreamGeometryContext ctx = lines.Open())
        {
            ctx.BeginFigure(new Point(d.CenterX, d.NeckY), isFilled: false);
            ctx.LineTo(new Point(d.CenterX, d.HipY));
            ctx.EndFigure(false);

            ctx.BeginFigure(new Point(d.CenterX - d.ArmHalf, d.ShoulderY), isFilled: false);
            ctx.LineTo(new Point(d.CenterX + d.ArmHalf, d.ShoulderY));
            ctx.EndFigure(false);

            ctx.BeginFigure(new Point(d.CenterX - d.LegSpread, d.FigureHeight), isFilled: false);
            ctx.LineTo(new Point(d.CenterX, d.HipY));
            ctx.LineTo(new Point(d.CenterX + d.LegSpread, d.FigureHeight));
            ctx.EndFigure(false);
        }

        group.Children.Add(lines);
        return group;
    }
}
