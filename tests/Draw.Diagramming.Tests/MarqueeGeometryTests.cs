using Draw.Diagramming.Geometry;
using Draw.Model.Primitives;
using Xunit;

namespace Draw.Diagramming.Tests;

public class MarqueeGeometryTests
{
    private static readonly Rect2D Box = new(0, 0, 100, 100);

    [Fact]
    public void Empty_IsNeverHit()
    {
        Assert.False(MarqueeGeometry.IntersectsPolyline(Box, System.Array.Empty<Point2D>()));
    }

    [Fact]
    public void SinglePoint_HitWhenInside()
    {
        Assert.True(MarqueeGeometry.IntersectsPolyline(Box, new[] { new Point2D(50, 50) }));
        Assert.False(MarqueeGeometry.IntersectsPolyline(Box, new[] { new Point2D(150, 50) }));
    }

    [Fact]
    public void FullyInside_IsHit()
    {
        Point2D[] line = { new(20, 20), new(80, 80) };
        Assert.True(MarqueeGeometry.IntersectsPolyline(Box, line));
    }

    [Fact]
    public void CrossesOneEdge_IsHit()
    {
        // Starts inside, ends well outside to the right.
        Point2D[] line = { new(50, 50), new(300, 50) };
        Assert.True(MarqueeGeometry.IntersectsPolyline(Box, line));
    }

    [Fact]
    public void PassesStraightThrough_BothEndpointsOutside_IsHit()
    {
        // Horizontal line crossing the box left-to-right; neither endpoint is inside.
        Point2D[] line = { new(-50, 50), new(200, 50) };
        Assert.True(MarqueeGeometry.IntersectsPolyline(Box, line));
    }

    [Fact]
    public void FullyOutside_NotHit()
    {
        Point2D[] line = { new(200, 200), new(300, 300) };
        Assert.False(MarqueeGeometry.IntersectsPolyline(Box, line));
    }

    [Fact]
    public void ParallelOutside_NotHit()
    {
        // Horizontal segment entirely below the box.
        Point2D[] line = { new(-50, 200), new(200, 200) };
        Assert.False(MarqueeGeometry.IntersectsPolyline(Box, line));
    }

    [Fact]
    public void TouchesCorner_IsHit()
    {
        // Segment ending exactly on the bottom-right corner.
        Point2D[] line = { new(200, 200), new(100, 100) };
        Assert.True(MarqueeGeometry.IntersectsPolyline(Box, line));
    }

    [Fact]
    public void MultiSegmentPolyline_HitWhenAnySegmentCrosses()
    {
        // First two segments stay outside (above); the last dips through the box.
        Point2D[] poly = { new(-50, -50), new(200, -50), new(50, 50) };
        Assert.True(MarqueeGeometry.IntersectsPolyline(Box, poly));
    }

    [Fact]
    public void NegativeSizeRectangle_IsNormalized()
    {
        // Rectangle dragged bottom-right to top-left (negative width/height) still covers the same area.
        Rect2D dragged = new(100, 100, -100, -100);
        Assert.True(MarqueeGeometry.IntersectsPolyline(dragged, new[] { new Point2D(50, 50) }));
    }
}
