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

    [Fact]
    public void DashedOutlines_FewerThanTwoPoints_ReturnsEmpty()
        => Assert.Empty(TaperedStroke.BuildDashedOutlines(
            new[] { new Point2D(0, 0) }, 10, 2, new double[] { 20, 10 }));

    [Fact]
    public void DashedOutlines_EmptyPattern_ReturnsSingleSolidOutline()
    {
        IReadOnlyList<Point2D> centerline = new[] { new Point2D(0, 0), new Point2D(50, 0), new Point2D(100, 0) };
        IReadOnlyList<Point2D> solid = TaperedStroke.BuildOutline(centerline, 10, 2);
        IReadOnlyList<IReadOnlyList<Point2D>> dashed =
            TaperedStroke.BuildDashedOutlines(centerline, 10, 2, System.Array.Empty<double>());

        IReadOnlyList<Point2D> only = Assert.Single(dashed);
        Assert.Equal(solid.Count, only.Count);
        for (int i = 0; i < solid.Count; i++)
        {
            Assert.True(only[i].ApproximatelyEquals(solid[i], 1e-9));
        }
    }

    [Fact]
    public void DashedOutlines_StraightLine_CutsIntoExpectedOnRuns()
    {
        IReadOnlyList<Point2D> centerline = new[] { new Point2D(0, 0), new Point2D(100, 0) };
        // on/off = 20/10 from the source ⇒ on runs [0,20] [30,50] [60,80] [90,100].
        IReadOnlyList<IReadOnlyList<Point2D>> dashed =
            TaperedStroke.BuildDashedOutlines(centerline, 10, 10, new double[] { 20, 10 });

        Assert.Equal(4, dashed.Count);
        foreach (IReadOnlyList<Point2D> outline in dashed)
        {
            Assert.True(outline.Count >= 4);
            Assert.Equal(0, outline.Count % 2); // a left run followed by its mirrored right run
            foreach (Point2D p in outline)
            {
                Assert.InRange(p.X, -1e-6, 100d + 1e-6);
                Assert.Equal(5d, System.Math.Abs(p.Y), 6); // uniform half-width
            }
        }
    }

    [Fact]
    public void DashedOutlines_TotalOnLength_MatchesThePattern()
    {
        IReadOnlyList<Point2D> centerline = new[] { new Point2D(0, 0), new Point2D(100, 0) };
        IReadOnlyList<IReadOnlyList<Point2D>> dashed =
            TaperedStroke.BuildDashedOutlines(centerline, 10, 10, new double[] { 20, 10 });

        double onLength = 0d;
        foreach (IReadOnlyList<Point2D> outline in dashed)
        {
            double min = double.MaxValue;
            double max = double.MinValue;
            foreach (Point2D p in outline)
            {
                min = System.Math.Min(min, p.X);
                max = System.Math.Max(max, p.X);
            }

            onLength += max - min;
        }

        Assert.Equal(70d, onLength, 6); // 20 + 20 + 20 + 10
    }

    [Fact]
    public void DashedOutlines_SliceWidth_FollowsTheTaperAtEachCut()
    {
        IReadOnlyList<Point2D> centerline = new[] { new Point2D(0, 0), new Point2D(100, 0) };
        // First on-run is [0,30]; the ribbon tapers 20 → 0, so full width at s is 20·(1 − s/100).
        IReadOnlyList<IReadOnlyList<Point2D>> dashed =
            TaperedStroke.BuildDashedOutlines(centerline, 20, 0, new double[] { 30, 10 });

        IReadOnlyList<Point2D> first = dashed[0]; // [left(0), left(30), right(30), right(0)]
        double startSpan = first[0].DistanceTo(first[3]);
        double endSpan = first[1].DistanceTo(first[2]);

        Assert.Equal(20d, startSpan, 6); // full width at the source
        Assert.Equal(14d, endSpan, 6);   // 20·(1 − 30/100)
    }
}
