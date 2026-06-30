using System;
using System.Collections.Generic;
using Draw.Diagramming.Geometry;
using Draw.Model.Primitives;

namespace Draw.Diagramming.Layout;

/// <summary>The side of a node's axis-aligned bounds that a connector attaches to.</summary>
public enum BoxSide
{
    Left,
    Top,
    Right,
    Bottom,
}

/// <summary>
/// Pure geometry for evenly spacing connector attachment points along a shape edge. UI-agnostic:
/// callers classify each attachment to a <see cref="BoxSide"/>, then ask for the relative (u,v)
/// anchor of each point. Works on bounds alone — the (u,v) is resolved to the outline elsewhere
/// (<c>ShapeBoundary.ResolveAnchor</c>), so this never needs the shape kind.
/// </summary>
public static class ConnectionDistributor
{
    /// <summary>
    /// The bounding-box side nearest to a (resolved) world attachment <paramref name="point"/>.
    /// Ties between a horizontal and vertical side resolve to the horizontal one; degenerate bounds
    /// fall back to <see cref="BoxSide.Left"/>.
    /// </summary>
    public static BoxSide ClassifySide(Rect2D bounds, Point2D point)
    {
        double rx = bounds.Width <= 0 ? 0.5 : Math.Clamp((point.X - bounds.X) / bounds.Width, 0d, 1d);
        double ry = bounds.Height <= 0 ? 0.5 : Math.Clamp((point.Y - bounds.Y) / bounds.Height, 0d, 1d);

        double horizontal = Math.Min(rx, 1d - rx);
        double vertical = Math.Min(ry, 1d - ry);

        if (horizontal <= vertical)
        {
            return rx <= 0.5 ? BoxSide.Left : BoxSide.Right;
        }

        return ry <= 0.5 ? BoxSide.Top : BoxSide.Bottom;
    }

    /// <summary>
    /// The position of <paramref name="point"/> along <paramref name="side"/> as a fraction in [0,1]
    /// (the coordinate that varies along that side). Used to keep the current order when spacing.
    /// </summary>
    public static double FractionAlong(BoxSide side, Rect2D bounds, Point2D point)
    {
        if (side is BoxSide.Left or BoxSide.Right)
        {
            return bounds.Height <= 0 ? 0.5 : Math.Clamp((point.Y - bounds.Y) / bounds.Height, 0d, 1d);
        }

        return bounds.Width <= 0 ? 0.5 : Math.Clamp((point.X - bounds.X) / bounds.Width, 0d, 1d);
    }

    /// <summary>
    /// The sort key that orders connector ends along <paramref name="side"/> by where their far end
    /// (<paramref name="otherEnd"/>) sits: the coordinate that varies along the side — Y for the vertical
    /// Left/Right edges, X for the horizontal Top/Bottom edges. Sorting ascending makes the attachment
    /// slots run in the same order as the connected shapes, so the connectors don't cross.
    /// </summary>
    private static double CrossingOrderKey(BoxSide side, Point2D otherEnd)
        => side is BoxSide.Left or BoxSide.Right ? otherEnd.Y : otherEnd.X;

    /// <summary>
    /// The relative (u,v) anchor in [0,1]² of the bounds for the <paramref name="index"/>-th of
    /// <paramref name="count"/> evenly spaced points on <paramref name="side"/>. Points sit at
    /// <c>(index+1)/(count+1)</c> of the side, so the gap to each corner equals the inter-point gap.
    /// </summary>
    public static Point2D EvenAnchor(BoxSide side, int index, int count)
    {
        double t = (index + 1d) / (count + 1d);
        return side switch
        {
            BoxSide.Left => new Point2D(0d, t),
            BoxSide.Right => new Point2D(1d, t),
            BoxSide.Top => new Point2D(t, 0d),
            _ => new Point2D(t, 1d),
        };
    }

    /// <summary>
    /// A connector end to be (re)pinned. <typeparamref name="TEnd"/> <paramref name="Token"/> identifies
    /// the end to the caller; the rest is a snapshot of where it currently attaches — the shape it touches
    /// (<paramref name="NodeId"/> + <paramref name="NodeBounds"/>), the current route point on that shape,
    /// a reference point for the connector's far end (<paramref name="OtherEnd"/> — the connected shape's
    /// centre, used to order the ends on a side so they don't cross), and its current relative anchor
    /// (<c>null</c> when the end is not pinned yet).
    /// </summary>
    public readonly record struct PinningEnd<TEnd>(
        TEnd Token, Guid NodeId, Rect2D NodeBounds, Point2D RoutePoint, Point2D OtherEnd, Point2D? CurrentAnchor);

    /// <summary>
    /// Plans (re)spaced anchors for a set of connector ends. Ends are grouped by the shape they touch and
    /// the <see cref="BoxSide"/> they currently land on; within each group they are ordered by where their
    /// far end (<see cref="PinningEnd{TEnd}.OtherEnd"/>) sits along that side, so the slots run in the same
    /// order as the connected shapes and the connectors don't cross (ties keep their current order, by
    /// <see cref="FractionAlong"/>), and <paramref name="anchorFor"/> chooses the relative (u,v) for the
    /// i-th of n. Only ends whose anchor actually changes are returned (an end already at its computed
    /// anchor is omitted), so an empty result means "nothing to do". Pure: reads the snapshot, mutates
    /// nothing — the caller applies the plan and owns undo.
    /// </summary>
    public static IReadOnlyList<(TEnd Token, Point2D Anchor)> PlanPinning<TEnd>(
        IReadOnlyList<PinningEnd<TEnd>> ends,
        Func<BoxSide, int, int, Point2D> anchorFor)
    {
        ArgumentNullException.ThrowIfNull(ends);
        ArgumentNullException.ThrowIfNull(anchorFor);

        Dictionary<(Guid Node, BoxSide Side), List<(PinningEnd<TEnd> End, double Fraction)>> groups = new();
        foreach (PinningEnd<TEnd> end in ends)
        {
            BoxSide side = ClassifySide(end.NodeBounds, end.RoutePoint);
            double fraction = FractionAlong(side, end.NodeBounds, end.RoutePoint);
            (Guid Node, BoxSide Side) key = (end.NodeId, side);
            if (!groups.TryGetValue(key, out List<(PinningEnd<TEnd> End, double Fraction)>? list))
            {
                list = new List<(PinningEnd<TEnd> End, double Fraction)>();
                groups[key] = list;
            }

            list.Add((end, fraction));
        }

        List<(TEnd Token, Point2D Anchor)> plan = new();
        foreach (KeyValuePair<(Guid Node, BoxSide Side), List<(PinningEnd<TEnd> End, double Fraction)>> group in groups)
        {
            List<(PinningEnd<TEnd> End, double Fraction)> list = group.Value;
            BoxSide side = group.Key.Side;
            list.Sort((a, b) =>
            {
                int byOther = CrossingOrderKey(side, a.End.OtherEnd).CompareTo(CrossingOrderKey(side, b.End.OtherEnd));
                return byOther != 0 ? byOther : a.Fraction.CompareTo(b.Fraction);
            });
            for (int i = 0; i < list.Count; i++)
            {
                Point2D anchor = anchorFor(side, i, list.Count);
                PinningEnd<TEnd> end = list[i].End;
                if (end.CurrentAnchor is { } current && current.ApproximatelyEquals(anchor, GeometryTolerance.Distance))
                {
                    continue; // already at the computed anchor — no change, no undo
                }

                plan.Add((end.Token, anchor));
            }
        }

        return plan;
    }
}
