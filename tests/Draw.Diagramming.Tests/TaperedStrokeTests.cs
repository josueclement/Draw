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
}
