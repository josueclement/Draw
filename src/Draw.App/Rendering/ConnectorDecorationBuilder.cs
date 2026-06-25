using System.Collections.Generic;
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

/// <summary>
/// UI-agnostic description of an end decoration in world coordinates: closed (fillable) polygons,
/// open (stroke-only) polylines, and rings. Both the Avalonia geometry builder and the SVG exporter
/// build from this so the two renderings cannot drift apart.
/// </summary>
public sealed class DecorationGeometryData
{
    /// <summary>Closed figures (triangle / diamond) — filled per the decoration's fill rule.</summary>
    public List<ModelPoint[]> ClosedPaths { get; } = new();

    /// <summary>Open figures (arrow barbs, crow's-foot prongs, the "one" bar) — stroked, never filled.</summary>
    public List<ModelPoint[]> OpenPaths { get; } = new();

    /// <summary>Rings (the crow's-foot "zero" marker) — stroked, hollow.</summary>
    public List<(ModelPoint Center, double Radius)> Circles { get; } = new();
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
            // Mind-map branches render as a filled tapered ribbon with no end caps and a solid edge.
            RelationshipKind.MindMapBranch => (ConnectorEndDecoration.None, ConnectorEndDecoration.None, false),
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
    /// Describes the decoration's geometry in world coordinates. <paramref name="direction"/> is the
    /// unit vector pointing along the line toward <paramref name="tip"/>. Returns <c>null</c> for
    /// <see cref="ConnectorEndDecoration.None"/>.
    /// </summary>
    public static DecorationGeometryData? Describe(ConnectorEndDecoration decoration, ModelPoint tip, ModelPoint direction)
    {
        if (decoration == ConnectorEndDecoration.None)
        {
            return null;
        }

        ModelPoint perp = new(-direction.Y, direction.X);
        DecorationGeometryData data = new();

        switch (decoration)
        {
            case ConnectorEndDecoration.OpenArrow:
                data.OpenPaths.Add(new[]
                {
                    tip - (direction * ArrowLength) + (perp * ArrowHalf),
                    tip,
                    tip - (direction * ArrowLength) - (perp * ArrowHalf),
                });
                break;
            case ConnectorEndDecoration.HollowTriangle:
                data.ClosedPaths.Add(new[]
                {
                    tip,
                    tip - (direction * TriangleLength) + (perp * TriangleHalf),
                    tip - (direction * TriangleLength) - (perp * TriangleHalf),
                });
                break;
            case ConnectorEndDecoration.FilledDiamond:
            case ConnectorEndDecoration.HollowDiamond:
                data.ClosedPaths.Add(new[]
                {
                    tip,
                    tip - (direction * (DiamondLength / 2)) + (perp * DiamondHalf),
                    tip - (direction * DiamondLength),
                    tip - (direction * (DiamondLength / 2)) - (perp * DiamondHalf),
                });
                break;
            case ConnectorEndDecoration.CrowOne:
                data.OpenPaths.Add(Bar(tip - (direction * BarOffset), perp));
                break;
            case ConnectorEndDecoration.CrowMany:
                AddCrowsFoot(data, tip, direction, perp);
                break;
            case ConnectorEndDecoration.CrowZeroOrOne:
                data.OpenPaths.Add(Bar(tip - (direction * BarOffset), perp));
                data.Circles.Add((tip - (direction * CircleOffset), CircleRadius));
                break;
            case ConnectorEndDecoration.CrowOneOrMany:
                AddCrowsFoot(data, tip, direction, perp);
                data.OpenPaths.Add(Bar(tip - (direction * (FootLength + BarHalf)), perp));
                break;
            case ConnectorEndDecoration.CrowZeroOrMany:
                AddCrowsFoot(data, tip, direction, perp);
                data.Circles.Add((tip - (direction * (FootLength + CircleRadius + 2d)), CircleRadius));
                break;
        }

        return data;
    }

    /// <summary>
    /// Builds the decoration geometry in world coordinates for Avalonia rendering, from
    /// <see cref="Describe(ConnectorEndDecoration, ModelPoint, ModelPoint)"/>.
    /// </summary>
    public static Geometry? Build(ConnectorEndDecoration decoration, ModelPoint tip, ModelPoint direction)
    {
        if (Describe(decoration, tip, direction) is not { } data)
        {
            return null;
        }

        GeometryGroup group = new();
        foreach (ModelPoint[] polygon in data.ClosedPaths)
        {
            group.Children.Add(Figure(polygon, closed: true));
        }

        foreach (ModelPoint[] polyline in data.OpenPaths)
        {
            group.Children.Add(Figure(polyline, closed: false));
        }

        foreach ((ModelPoint center, double radius) in data.Circles)
        {
            group.Children.Add(new EllipseGeometry(new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2)));
        }

        return group;
    }

    private static ModelPoint[] Bar(ModelPoint center, ModelPoint perp)
        => new[] { center + (perp * BarHalf), center - (perp * BarHalf) };

    // Three prongs splaying from a point back along the line to the shape edge — the crow's-foot "many".
    private static void AddCrowsFoot(DecorationGeometryData data, ModelPoint tip, ModelPoint dir, ModelPoint perp)
    {
        ModelPoint apex = tip - (dir * FootLength);
        data.OpenPaths.Add(new[] { apex, tip });
        data.OpenPaths.Add(new[] { apex, tip + (perp * FootHalf) });
        data.OpenPaths.Add(new[] { apex, tip - (perp * FootHalf) });
    }

    private static Geometry Figure(ModelPoint[] points, bool closed)
    {
        StreamGeometry geometry = new();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(points[0].X, points[0].Y), isFilled: closed);
            for (int i = 1; i < points.Length; i++)
            {
                ctx.LineTo(new Point(points[i].X, points[i].Y));
            }

            ctx.EndFigure(closed);
        }

        return geometry;
    }
}
