using Draw.Diagramming.Geometry;
using Draw.Model.Primitives;
using Xunit;

namespace Draw.Diagramming.Tests;

public class SegmentGeometryTests
{
    private static readonly Point2D A = new(0, 0);
    private static readonly Point2D B = new(10, 0);

    [Fact]
    public void DistanceToSegment_PointOnSegment_IsZero()
        => Assert.Equal(0d, SegmentGeometry.DistanceToSegment(new Point2D(5, 0), A, B));

    [Fact]
    public void DistanceToSegment_Perpendicular_ReturnsPerpendicularDistance()
        => Assert.Equal(5d, SegmentGeometry.DistanceToSegment(new Point2D(5, 5), A, B));

    [Fact]
    public void DistanceToSegment_BeyondEnd_ClampsToEnd()
        => Assert.Equal(10d, SegmentGeometry.DistanceToSegment(new Point2D(20, 0), A, B));

    [Fact]
    public void DistanceToSegment_BeforeStart_ClampsToStart()
        => Assert.Equal(5d, SegmentGeometry.DistanceToSegment(new Point2D(-5, 0), A, B));

    [Fact]
    public void DistanceToSegment_DegenerateSegment_ReturnsDistanceToStart()
    {
        Point2D p = new(3, 4);
        Point2D coincident = new(0, 0);
        Assert.Equal(5d, SegmentGeometry.DistanceToSegment(p, coincident, coincident)); // 3-4-5 triangle
    }

    [Fact]
    public void DistanceToPolyline_ReturnsMinimumOverSegments()
    {
        Point2D[] polyline = { new(0, 0), new(10, 0), new(10, 10) };
        // (12,5) is nearest the vertical second segment (x=10), distance 2.
        Assert.Equal(2d, SegmentGeometry.DistanceToPolyline(new Point2D(12, 5), polyline));
    }

    [Fact]
    public void DistanceToPolyline_FewerThanTwoPoints_IsPositiveInfinity()
    {
        Assert.Equal(double.PositiveInfinity, SegmentGeometry.DistanceToPolyline(new Point2D(0, 0), new Point2D[] { new(1, 1) }));
        Assert.Equal(double.PositiveInfinity, SegmentGeometry.DistanceToPolyline(new Point2D(0, 0), System.Array.Empty<Point2D>()));
    }

    [Fact]
    public void NearestSegmentIndex_ReturnsZeroBasedSegmentIndex()
    {
        Point2D[] polyline = { new(0, 0), new(10, 0), new(10, 10) };
        Assert.Equal(0, SegmentGeometry.NearestSegmentIndex(new Point2D(5, -2), polyline)); // first segment
        Assert.Equal(1, SegmentGeometry.NearestSegmentIndex(new Point2D(12, 5), polyline)); // second segment
    }

    [Fact]
    public void NearestSegmentIndex_FewerThanTwoPoints_ReturnsZero()
        => Assert.Equal(0, SegmentGeometry.NearestSegmentIndex(new Point2D(0, 0), new Point2D[] { new(1, 1) }));
}
