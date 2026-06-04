using System.Collections.Generic;
using System.Linq;
using Draw.Model.Primitives;

namespace Draw.Diagramming.Layout;

/// <summary>Which edge or center the selected shapes line up on.</summary>
public enum AlignmentMode
{
    Left,
    CenterHorizontal,
    Right,
    Top,
    CenterVertical,
    Bottom,
}

/// <summary>The axis along which selected shapes are evenly spaced.</summary>
public enum DistributionMode
{
    Horizontal,
    Vertical,
}

/// <summary>
/// Pure geometry for aligning and distributing a set of rectangles. UI-agnostic: callers pass the
/// current bounds and apply the returned positions. Only the position changes — sizes are preserved —
/// and the result is in the same order as the input so callers can map it back by index.
/// </summary>
public static class ShapeArranger
{
    /// <summary>
    /// Moves every rectangle so it lines up with <paramref name="mode"/> relative to the union
    /// bounding box of the whole set. A no-op when fewer than two rectangles are supplied.
    /// </summary>
    public static IReadOnlyList<Rect2D> Align(IReadOnlyList<Rect2D> rects, AlignmentMode mode)
    {
        Rect2D[] result = rects.ToArray();
        if (result.Length < 2)
        {
            return result;
        }

        Rect2D box = result[0];
        for (int i = 1; i < result.Length; i++)
        {
            box = box.Union(result[i]);
        }

        for (int i = 0; i < result.Length; i++)
        {
            Rect2D r = result[i];
            result[i] = mode switch
            {
                AlignmentMode.Left => r with { X = box.Left },
                AlignmentMode.CenterHorizontal => r with { X = box.Center.X - (r.Width / 2) },
                AlignmentMode.Right => r with { X = box.Right - r.Width },
                AlignmentMode.Top => r with { Y = box.Top },
                AlignmentMode.CenterVertical => r with { Y = box.Center.Y - (r.Height / 2) },
                AlignmentMode.Bottom => r with { Y = box.Bottom - r.Height },
                _ => r,
            };
        }

        return result;
    }

    /// <summary>
    /// Evens out the edge-to-edge gaps between rectangles along <paramref name="mode"/>. The two
    /// outermost shapes stay put; the inner ones are repositioned so all gaps are equal. A no-op when
    /// fewer than three rectangles are supplied (nothing to redistribute). Gaps may be negative if the
    /// shapes overlap — the spacing is still made uniform.
    /// </summary>
    public static IReadOnlyList<Rect2D> Distribute(IReadOnlyList<Rect2D> rects, DistributionMode mode)
    {
        Rect2D[] result = rects.ToArray();
        int n = result.Length;
        if (n < 3)
        {
            return result;
        }

        bool horizontal = mode == DistributionMode.Horizontal;

        // Order by leading edge; the first and last in that order are the fixed anchors.
        int[] order = Enumerable.Range(0, n)
            .OrderBy(i => horizontal ? result[i].Left : result[i].Top)
            .ToArray();

        double extent(Rect2D r) => horizontal ? r.Width : r.Height;
        double lead(Rect2D r) => horizontal ? r.Left : r.Top;
        double trail(Rect2D r) => horizontal ? r.Right : r.Bottom;

        double sumExtents = 0d;
        for (int i = 0; i < n; i++)
        {
            sumExtents += extent(result[i]);
        }

        double span = trail(result[order[n - 1]]) - lead(result[order[0]]);
        double gap = (span - sumExtents) / (n - 1);

        // Walk the inner shapes left-to-right (or top-to-bottom), leaving the anchors untouched.
        double cursor = lead(result[order[0]]) + extent(result[order[0]]) + gap;
        for (int k = 1; k < n - 1; k++)
        {
            int idx = order[k];
            Rect2D r = result[idx];
            result[idx] = horizontal ? r with { X = cursor } : r with { Y = cursor };
            cursor += extent(r) + gap;
        }

        return result;
    }
}
