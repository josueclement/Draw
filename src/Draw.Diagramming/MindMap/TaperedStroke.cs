using System;
using System.Collections.Generic;
using Draw.Model.Primitives;

namespace Draw.Diagramming.MindMap;

/// <summary>
/// Pure geometry that turns a connector centerline into a closed, variable-width ribbon outline:
/// each sample point is offset by ±half-width along its normal, with the width interpolated by arc
/// length from <c>startWidth</c> (the source/parent end) to <c>endWidth</c> (the target/child end).
/// The result is one closed polygon — the left edge forward then the right edge back — which the
/// on-canvas and SVG renderers fill. UI-agnostic, so it is shared by both and unit-tested headless.
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
        if (centerline.Count < 2)
        {
            return Array.Empty<Point2D>();
        }

        double[] cumulative = new double[centerline.Count];
        for (int i = 1; i < centerline.Count; i++)
        {
            cumulative[i] = cumulative[i - 1] + centerline[i].DistanceTo(centerline[i - 1]);
        }

        double total = cumulative[centerline.Count - 1];
        if (total <= Point2D.ZeroLengthTolerance)
        {
            return Array.Empty<Point2D>();
        }

        double halfStart = Math.Max(0d, startWidth) / 2d;
        double halfEnd = Math.Max(0d, endWidth) / 2d;

        int last = centerline.Count - 1;
        Point2D[] left = new Point2D[centerline.Count];
        Point2D[] right = new Point2D[centerline.Count];
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

        List<Point2D> outline = new(centerline.Count * 2);
        for (int i = 0; i < left.Length; i++)
        {
            outline.Add(left[i]);
        }

        for (int i = right.Length - 1; i >= 0; i--)
        {
            outline.Add(right[i]);
        }

        return outline;
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
