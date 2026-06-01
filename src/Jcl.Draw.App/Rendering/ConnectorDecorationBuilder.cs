using Avalonia;
using Avalonia.Media;
using Jcl.Draw.Model.Connectors;
using ModelPoint = Jcl.Draw.Model.Primitives.Point2D;

namespace Jcl.Draw.App.Rendering;

/// <summary>The end-cap decoration drawn at a connector endpoint.</summary>
public enum ConnectorEndDecoration
{
    None,
    OpenArrow,
    HollowTriangle,
    FilledDiamond,
    HollowDiamond,
}

/// <summary>Maps UML relationship kinds to line style and end decorations, and builds their geometry.</summary>
public static class ConnectorDecorationBuilder
{
    private const double ArrowLength = 13d;
    private const double ArrowHalf = 6d;
    private const double TriangleLength = 15d;
    private const double TriangleHalf = 8d;
    private const double DiamondLength = 18d;
    private const double DiamondHalf = 7d;

    /// <summary>Returns the decoration at each end and whether the line is dashed.</summary>
    public static (ConnectorEndDecoration Source, ConnectorEndDecoration Target, bool Dashed) Describe(RelationshipKind kind)
        => kind switch
        {
            RelationshipKind.Association => (ConnectorEndDecoration.None, ConnectorEndDecoration.None, false),
            RelationshipKind.DirectedAssociation => (ConnectorEndDecoration.None, ConnectorEndDecoration.OpenArrow, false),
            RelationshipKind.Aggregation => (ConnectorEndDecoration.HollowDiamond, ConnectorEndDecoration.None, false),
            RelationshipKind.Composition => (ConnectorEndDecoration.FilledDiamond, ConnectorEndDecoration.None, false),
            RelationshipKind.Generalization => (ConnectorEndDecoration.None, ConnectorEndDecoration.HollowTriangle, false),
            RelationshipKind.Realization => (ConnectorEndDecoration.None, ConnectorEndDecoration.HollowTriangle, true),
            RelationshipKind.Dependency => (ConnectorEndDecoration.None, ConnectorEndDecoration.OpenArrow, true),
            RelationshipKind.Include => (ConnectorEndDecoration.None, ConnectorEndDecoration.OpenArrow, true),
            RelationshipKind.Extend => (ConnectorEndDecoration.None, ConnectorEndDecoration.OpenArrow, true),
            _ => (ConnectorEndDecoration.None, ConnectorEndDecoration.None, false),
        };

    public static bool IsFilled(ConnectorEndDecoration decoration) => decoration == ConnectorEndDecoration.FilledDiamond;

    public static bool IsOpen(ConnectorEndDecoration decoration) => decoration == ConnectorEndDecoration.OpenArrow;

    /// <summary>
    /// Builds the decoration geometry in world coordinates. <paramref name="direction"/> is the
    /// unit vector pointing along the line toward <paramref name="tip"/>.
    /// </summary>
    public static Geometry? Build(ConnectorEndDecoration decoration, ModelPoint tip, ModelPoint direction)
    {
        if (decoration == ConnectorEndDecoration.None)
        {
            return null;
        }

        ModelPoint perpendicular = new(-direction.Y, direction.X);

        return decoration switch
        {
            ConnectorEndDecoration.OpenArrow => OpenArrow(tip, direction, perpendicular),
            ConnectorEndDecoration.HollowTriangle => Triangle(tip, direction, perpendicular),
            ConnectorEndDecoration.FilledDiamond or ConnectorEndDecoration.HollowDiamond => Diamond(tip, direction, perpendicular),
            _ => null,
        };
    }

    private static Geometry OpenArrow(ModelPoint tip, ModelPoint dir, ModelPoint perp)
    {
        ModelPoint barb1 = tip - (dir * ArrowLength) + (perp * ArrowHalf);
        ModelPoint barb2 = tip - (dir * ArrowLength) - (perp * ArrowHalf);
        StreamGeometry geometry = new();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(ToPoint(barb1), isFilled: false);
            ctx.LineTo(ToPoint(tip));
            ctx.LineTo(ToPoint(barb2));
            ctx.EndFigure(false);
        }

        return geometry;
    }

    private static Geometry Triangle(ModelPoint tip, ModelPoint dir, ModelPoint perp)
    {
        ModelPoint b1 = tip - (dir * TriangleLength) + (perp * TriangleHalf);
        ModelPoint b2 = tip - (dir * TriangleLength) - (perp * TriangleHalf);
        return ClosedFigure(tip, b1, b2);
    }

    private static Geometry Diamond(ModelPoint tip, ModelPoint dir, ModelPoint perp)
    {
        ModelPoint side1 = tip - (dir * (DiamondLength / 2)) + (perp * DiamondHalf);
        ModelPoint back = tip - (dir * DiamondLength);
        ModelPoint side2 = tip - (dir * (DiamondLength / 2)) - (perp * DiamondHalf);
        return ClosedFigure(tip, side1, back, side2);
    }

    private static Geometry ClosedFigure(params ModelPoint[] points)
    {
        StreamGeometry geometry = new();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(ToPoint(points[0]), isFilled: true);
            for (int i = 1; i < points.Length; i++)
            {
                ctx.LineTo(ToPoint(points[i]));
            }

            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Point ToPoint(ModelPoint p) => new(p.X, p.Y);
}
