using Avalonia;
using Avalonia.Media;
using Draw.Model.Connectors;
using ModelPoint = Draw.Model.Primitives.Point2D;

namespace Draw.App.Rendering;

/// <summary>The end-cap decoration drawn at a connector endpoint.</summary>
public enum ConnectorEndDecoration
{
    None,
    OpenArrow,
    HollowTriangle,
    FilledDiamond,
    HollowDiamond,

    // ER crow's-foot cardinality symbols (stroke-only; never filled).
    CrowOne,
    CrowMany,
    CrowZeroOrOne,
    CrowOneOrMany,
    CrowZeroOrMany,
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
    private const double FootLength = 14d;
    private const double FootHalf = 7d;
    private const double BarHalf = 6d;
    private const double BarOffset = 9d;
    private const double CircleRadius = 4d;
    private const double CircleOffset = 18d;

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

    /// <summary>The crow's-foot end symbol for an ER cardinality (<see cref="Cardinality.Unspecified"/> ⇒ none).</summary>
    public static ConnectorEndDecoration FromCardinality(Cardinality cardinality) => cardinality switch
    {
        Cardinality.One => ConnectorEndDecoration.CrowOne,
        Cardinality.Many => ConnectorEndDecoration.CrowMany,
        Cardinality.ZeroOrOne => ConnectorEndDecoration.CrowZeroOrOne,
        Cardinality.OneOrMany => ConnectorEndDecoration.CrowOneOrMany,
        Cardinality.ZeroOrMany => ConnectorEndDecoration.CrowZeroOrMany,
        _ => ConnectorEndDecoration.None,
    };

    public static bool IsFilled(ConnectorEndDecoration decoration) => decoration == ConnectorEndDecoration.FilledDiamond;

    public static bool IsOpen(ConnectorEndDecoration decoration) => decoration == ConnectorEndDecoration.OpenArrow;

    /// <summary>True for decorations drawn with the stroke only (no fill): open arrow + every crow's-foot symbol.</summary>
    public static bool IsStrokeOnly(ConnectorEndDecoration decoration) => decoration is ConnectorEndDecoration.None
        or ConnectorEndDecoration.OpenArrow
        or ConnectorEndDecoration.CrowOne
        or ConnectorEndDecoration.CrowMany
        or ConnectorEndDecoration.CrowZeroOrOne
        or ConnectorEndDecoration.CrowOneOrMany
        or ConnectorEndDecoration.CrowZeroOrMany;

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
            ConnectorEndDecoration.CrowOne => Bar(tip - (direction * BarOffset), perpendicular),
            ConnectorEndDecoration.CrowMany => CrowsFoot(tip, direction, perpendicular),
            ConnectorEndDecoration.CrowZeroOrOne => Group(Bar(tip - (direction * BarOffset), perpendicular), Circle(tip - (direction * CircleOffset))),
            ConnectorEndDecoration.CrowOneOrMany => Group(CrowsFoot(tip, direction, perpendicular), Bar(tip - (direction * (FootLength + BarHalf)), perpendicular)),
            ConnectorEndDecoration.CrowZeroOrMany => Group(CrowsFoot(tip, direction, perpendicular), Circle(tip - (direction * (FootLength + CircleRadius + 2d)))),
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

    // A single tick perpendicular to the line through the given point — the crow's-foot "one".
    private static Geometry Bar(ModelPoint center, ModelPoint perp)
    {
        StreamGeometry geometry = new();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(ToPoint(center + (perp * BarHalf)), isFilled: false);
            ctx.LineTo(ToPoint(center - (perp * BarHalf)));
            ctx.EndFigure(false);
        }

        return geometry;
    }

    // Three prongs splaying from a point back along the line to the shape edge — the crow's-foot "many".
    private static Geometry CrowsFoot(ModelPoint tip, ModelPoint dir, ModelPoint perp)
    {
        ModelPoint apex = tip - (dir * FootLength);
        StreamGeometry geometry = new();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            Prong(ctx, apex, tip);
            Prong(ctx, apex, tip + (perp * FootHalf));
            Prong(ctx, apex, tip - (perp * FootHalf));
        }

        return geometry;
    }

    private static void Prong(StreamGeometryContext ctx, ModelPoint from, ModelPoint to)
    {
        ctx.BeginFigure(ToPoint(from), isFilled: false);
        ctx.LineTo(ToPoint(to));
        ctx.EndFigure(false);
    }

    // A hollow ring on the line — the crow's-foot "zero" (optional) marker.
    private static Geometry Circle(ModelPoint center)
        => new EllipseGeometry(new Rect(center.X - CircleRadius, center.Y - CircleRadius, CircleRadius * 2, CircleRadius * 2));

    private static Geometry Group(params Geometry[] parts)
    {
        GeometryGroup group = new();
        foreach (Geometry part in parts)
        {
            group.Children.Add(part);
        }

        return group;
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
