using System;
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
}
