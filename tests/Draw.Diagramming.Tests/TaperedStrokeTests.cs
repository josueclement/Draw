using System.Collections.Generic;
using Draw.Diagramming.MindMap;
using Draw.Model.Primitives;
using Xunit;

namespace Draw.Diagramming.Tests;

public class TaperedStrokeTests
{
    [Fact]
    public void FewerThanTwoPoints_ReturnsEmpty()
        => Assert.Empty(TaperedStroke.BuildOutline(new[] { new Point2D(0, 0) }, 10, 2));

    [Fact]
    public void ZeroLengthCenterline_ReturnsEmpty()
        => Assert.Empty(TaperedStroke.BuildOutline(new[] { new Point2D(5, 5), new Point2D(5, 5) }, 10, 2));

    [Fact]
    public void Outline_HasTwiceTheSampleCount()
    {
        IReadOnlyList<Point2D> centerline = new[] { new Point2D(0, 0), new Point2D(50, 0), new Point2D(100, 0) };
        IReadOnlyList<Point2D> outline = TaperedStroke.BuildOutline(centerline, 10, 2);
        Assert.Equal(centerline.Count * 2, outline.Count);
    }

    [Fact]
    public void Outline_IsSymmetricAboutCenterline()
    {
        IReadOnlyList<Point2D> centerline = new[] { new Point2D(0, 0), new Point2D(50, 10), new Point2D(100, 0) };
        IReadOnlyList<Point2D> outline = TaperedStroke.BuildOutline(centerline, 8, 4);
        int n = centerline.Count;

        // outline = left[0..n-1] then right[n-1..0]; midpoint of each left/right pair is the centerline.
        for (int i = 0; i < n; i++)
        {
            Point2D left = outline[i];
            Point2D right = outline[(2 * n) - 1 - i];
            Point2D midpoint = (left + right) * 0.5d;
            Assert.True(midpoint.ApproximatelyEquals(centerline[i], 1e-6));
        }
    }

    [Fact]
    public void RibbonWidth_MatchesRequestedWidthsAtEachEnd()
    {
        IReadOnlyList<Point2D> centerline = new[] { new Point2D(0, 0), new Point2D(100, 0) };
        const double startWidth = 12d;
        const double endWidth = 3d;
        IReadOnlyList<Point2D> outline = TaperedStroke.BuildOutline(centerline, startWidth, endWidth);
        int n = centerline.Count;

        double startSpan = outline[0].DistanceTo(outline[(2 * n) - 1]); // left[0] vs right[0]
        double endSpan = outline[n - 1].DistanceTo(outline[n]);          // left[n-1] vs right[n-1]

        Assert.Equal(startWidth, startSpan, 6);
        Assert.Equal(endWidth, endSpan, 6);
    }

    // A bending centerline: the default endpoint tangents would slant the caps. The explicit-tangent
    // overrides below must square each cap to the supplied direction regardless of the curve.
    private static readonly IReadOnlyList<Point2D> Bending =
        new[] { new Point2D(0, 0), new Point2D(10, 10), new Point2D(20, 30) };

    [Fact]
    public void StartTangent_SquaresStartCapToTheGivenDirection()
    {
        IReadOnlyList<Point2D> outline = TaperedStroke.BuildOutline(Bending, 8, 4, startTangent: new Point2D(1, 0));
        int n = Bending.Count;

        // Start cap = left[0] (outline[0]) − right[0] (outline[2n-1]); ⟂ (1,0) ⇒ vertical, span = startWidth.
        Point2D cap = outline[0] - outline[(2 * n) - 1];
        Assert.Equal(0d, cap.X, 6);
        Assert.Equal(8d, System.Math.Abs(cap.Y), 6);
    }

    [Fact]
    public void EndTangent_SquaresEndCapToTheGivenDirection()
    {
        IReadOnlyList<Point2D> outline = TaperedStroke.BuildOutline(Bending, 8, 4, endTangent: new Point2D(0, 1));
        int n = Bending.Count;

        // End cap = left[n-1] (outline[n-1]) − right[n-1] (outline[n]); ⟂ (0,1) ⇒ horizontal, span = endWidth.
        Point2D cap = outline[n - 1] - outline[n];
        Assert.Equal(0d, cap.Y, 6);
        Assert.Equal(4d, System.Math.Abs(cap.X), 6);
    }

    [Fact]
    public void OmittedTangents_KeepFiniteDifferenceCaps()
    {
        // Without overrides the start cap follows the (1,1) opening segment, so it is not axis-aligned —
        // the slanted base this fix is meant to override.
        IReadOnlyList<Point2D> outline = TaperedStroke.BuildOutline(Bending, 8, 4);
        int n = Bending.Count;
        Point2D cap = outline[0] - outline[(2 * n) - 1];
        Assert.True(System.Math.Abs(cap.X) > 1d);
    }
}
