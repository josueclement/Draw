using System;
using System.Collections.Generic;
using Draw.Model.Primitives;

namespace Draw.Diagramming.MindMap;

/// <summary>
/// Pure geometry that turns a connector centerline into a closed, variable-width ribbon outline:
/// each sample point is offset by ±half-width along its normal, with the width interpolated by arc
/// length from <c>startWidth</c> (the source/parent end) to <c>endWidth</c> (the target/child end).
/// <see cref="BuildOutline"/> returns one closed polygon (left edge forward then right edge back);
/// <see cref="BuildDashedOutlines"/> returns that same ribbon cut into one polygon per dash run.
/// UI-agnostic, so both the on-canvas and SVG renderers share it and it is unit-tested headless.
/// </summary>
public static class TaperedStroke
{
    /// <summary>
    /// The closed outline of a tapered ribbon around <paramref name="centerline"/>. <paramref name="startWidth"/>
    /// and <paramref name="endWidth"/> are full stroke widths (the polygon spans ±width/2 about the
    /// centerline). Returns an empty list for a degenerate centerline (fewer than two points or zero
    /// total length). The returned outline has exactly <c>2 × centerline.Count</c> points.
    /// <para>
    /// <paramref name="startTangent"/>/<paramref name="endTangent"/>, when supplied, override the
    /// finite-difference tangent at the first/last sample — pass each end's forward direction (out of
    /// the source, into the target) so that end's flat cap squares to that direction (e.g. flush with
    /// a node edge) instead of to the bending curve. Each must point the same way the centerline
    /// travels there, or the offset would flip sides and pinch the ribbon end.
    /// </para>
    /// </summary>
    public static IReadOnlyList<Point2D> BuildOutline(
        IReadOnlyList<Point2D> centerline, double startWidth, double endWidth,
        Point2D? startTangent = null, Point2D? endTangent = null)
    {
        ArgumentNullException.ThrowIfNull(centerline);
        if (!TryComputeEdges(centerline, startWidth, endWidth, startTangent, endTangent,
                out Point2D[] left, out Point2D[] right, out _, out _))
        {
            return Array.Empty<Point2D>();
        }

        return Assemble(left, right);
    }

    /// <summary>
    /// The same tapered ribbon as <see cref="BuildOutline"/>, chopped along its length into one closed
    /// outline per "on" run of <paramref name="dashPattern"/> — a list of on/off run lengths in world
    /// units (on first), cycled from the source end. Each returned polygon is a slice of the continuous
    /// ribbon (same edges, same taper), with flat caps where it is cut. Returns a single outline
    /// (equivalent to <see cref="BuildOutline"/>) when the pattern is null/empty or has no positive
    /// run, and an empty list for a degenerate centerline. Never throws on a malformed pattern.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<Point2D>> BuildDashedOutlines(
        IReadOnlyList<Point2D> centerline, double startWidth, double endWidth,
        IReadOnlyList<double> dashPattern,
        Point2D? startTangent = null, Point2D? endTangent = null)
    {
        ArgumentNullException.ThrowIfNull(centerline);
        if (!TryComputeEdges(centerline, startWidth, endWidth, startTangent, endTangent,
                out Point2D[] left, out Point2D[] right, out double[] cumulative, out double total))
        {
            return Array.Empty<IReadOnlyList<Point2D>>();
        }

        // No usable pattern → one continuous ribbon (matches BuildOutline).
        if (dashPattern is null || dashPattern.Count == 0 || !HasPositiveRun(dashPattern))
        {
            return new List<IReadOnlyList<Point2D>> { Assemble(left, right) };
        }

        List<IReadOnlyList<Point2D>> segments = new();
        double s = 0d;
        int index = 0;
        bool on = true;
        while (s < total - Point2D.ZeroLengthTolerance)
        {
            double run = dashPattern[index % dashPattern.Count];
            if (run <= Point2D.ZeroLengthTolerance)
            {
                // Guard against a zero-length run stalling the walk; step by the tolerance instead.
                run = Point2D.ZeroLengthTolerance;
            }

            double end = Math.Min(s + run, total);
            if (on && end - s > Point2D.ZeroLengthTolerance)
            {
                segments.Add(BuildSlice(left, right, cumulative, s, end));
            }

            s = end;
            on = !on;
            index++;
        }

        return segments;
    }

    // Computes the ribbon's left/right edge samples and their cumulative arc lengths. Returns false for
    // a degenerate centerline (fewer than two points or zero total length), matching BuildOutline's
    // empty-list contract. Shared so the solid and dashed builders offset points identically.
    private static bool TryComputeEdges(
        IReadOnlyList<Point2D> centerline, double startWidth, double endWidth,
        Point2D? startTangent, Point2D? endTangent,
        out Point2D[] left, out Point2D[] right, out double[] cumulative, out double total)
    {
        left = Array.Empty<Point2D>();
        right = Array.Empty<Point2D>();
        cumulative = Array.Empty<double>();
        total = 0d;

        if (centerline.Count < 2)
        {
            return false;
        }

        cumulative = new double[centerline.Count];
        for (int i = 1; i < centerline.Count; i++)
        {
            cumulative[i] = cumulative[i - 1] + centerline[i].DistanceTo(centerline[i - 1]);
        }

        total = cumulative[centerline.Count - 1];
        if (total <= Point2D.ZeroLengthTolerance)
        {
            return false;
        }

        double halfStart = Math.Max(0d, startWidth) / 2d;
        double halfEnd = Math.Max(0d, endWidth) / 2d;

        int last = centerline.Count - 1;
        left = new Point2D[centerline.Count];
        right = new Point2D[centerline.Count];
        for (int i = 0; i < centerline.Count; i++)
        {
            Point2D tangent = EndpointOrSampled(centerline, i, last, startTangent, endTangent);
            // Left normal in y-down screen space (rotate the tangent +90°).
            Point2D normal = new(-tangent.Y, tangent.X);
            double t = cumulative[i] / total;
            double half = halfStart + ((halfEnd - halfStart) * t);
            left[i] = centerline[i] + (normal * half);
            right[i] = centerline[i] - (normal * half);
        }

        return true;
    }

    // A closed outline from a left edge (forward) followed by its right edge (backward).
    private static List<Point2D> Assemble(IReadOnlyList<Point2D> left, IReadOnlyList<Point2D> right)
    {
        List<Point2D> outline = new(left.Count + right.Count);
        for (int i = 0; i < left.Count; i++)
        {
            outline.Add(left[i]);
        }

        for (int i = right.Count - 1; i >= 0; i--)
        {
            outline.Add(right[i]);
        }

        return outline;
    }

    // One dash polygon: the ribbon edges sliced to the arc-length window [s0, s1].
    private static List<Point2D> BuildSlice(
        Point2D[] left, Point2D[] right, double[] cumulative, double s0, double s1)
        => Assemble(SliceEdge(left, cumulative, s0, s1), SliceEdge(right, cumulative, s0, s1));

    // The run of one edge between arc lengths s0 and s1: interpolated cut point at s0, every original
    // sample strictly inside the window, then the interpolated cut point at s1.
    private static List<Point2D> SliceEdge(Point2D[] edge, double[] cumulative, double s0, double s1)
    {
        List<Point2D> run = new() { InterpolateAt(edge, cumulative, s0) };
        for (int k = 0; k < cumulative.Length; k++)
        {
            if (cumulative[k] > s0 + Point2D.ZeroLengthTolerance && cumulative[k] < s1 - Point2D.ZeroLengthTolerance)
            {
                run.Add(edge[k]);
            }
        }

        run.Add(InterpolateAt(edge, cumulative, s1));
        return run;
    }

    // Linearly interpolates an edge point at arc length s (clamped to the edge's extent).
    private static Point2D InterpolateAt(Point2D[] edge, double[] cumulative, double s)
    {
        int last = cumulative.Length - 1;
        if (s <= cumulative[0])
        {
            return edge[0];
        }

        if (s >= cumulative[last])
        {
            return edge[last];
        }

        for (int k = 1; k <= last; k++)
        {
            if (s <= cumulative[k])
            {
                double span = cumulative[k] - cumulative[k - 1];
                double t = span <= Point2D.ZeroLengthTolerance ? 0d : (s - cumulative[k - 1]) / span;
                return edge[k - 1] + ((edge[k] - edge[k - 1]) * t);
            }
        }

        return edge[last];
    }

    private static bool HasPositiveRun(IReadOnlyList<double> dashPattern)
    {
        for (int i = 0; i < dashPattern.Count; i++)
        {
            if (dashPattern[i] > Point2D.ZeroLengthTolerance)
            {
                return true;
            }
        }

        return false;
    }

    // The caller-supplied endpoint tangent (so a ribbon end can square to a node edge rather than to
    // the bending curve), falling back to the finite-difference tangent when none is given or it is
    // degenerate.
    private static Point2D EndpointOrSampled(
        IReadOnlyList<Point2D> points, int i, int last, Point2D? startTangent, Point2D? endTangent)
    {
        Point2D? forced = i == 0 ? startTangent : i == last ? endTangent : null;
        if (forced is { } tangent)
        {
            Point2D unit = tangent.Normalized();
            if (unit.Length > Point2D.ZeroLengthTolerance)
            {
                return unit;
            }
        }

        return Tangent(points, i);
    }

    // Unit tangent at sample i via central (one-sided at the ends) finite differences.
    private static Point2D Tangent(IReadOnlyList<Point2D> points, int i)
    {
        Point2D delta;
        if (i == 0)
        {
            delta = points[1] - points[0];
        }
        else if (i == points.Count - 1)
        {
            delta = points[i] - points[i - 1];
        }
        else
        {
            delta = points[i + 1] - points[i - 1];
        }

        Point2D unit = delta.Normalized();
        return unit.Length <= Point2D.ZeroLengthTolerance ? new Point2D(1d, 0d) : unit;
    }
}
