using Draw.Diagramming.Geometry;
using Draw.Model.Primitives;
using Xunit;

namespace Draw.Diagramming.Tests;

public class NodeHandleGeometryTests
{
    private static readonly Rect2D Box = new(0, 0, 100, 100);

    [Fact]
    public void HandlePositions_AreInCanonicalClockwiseOrder()
    {
        Point2D[] p = NodeHandleGeometry.HandlePositions(Box);
        Assert.Equal(8, p.Length);
        Assert.Equal(new Point2D(0, 0), p[(int)NodeHandle.NW]);
        Assert.Equal(new Point2D(50, 0), p[(int)NodeHandle.N]);
        Assert.Equal(new Point2D(100, 0), p[(int)NodeHandle.NE]);
        Assert.Equal(new Point2D(100, 50), p[(int)NodeHandle.E]);
        Assert.Equal(new Point2D(100, 100), p[(int)NodeHandle.SE]);
        Assert.Equal(new Point2D(50, 100), p[(int)NodeHandle.S]);
        Assert.Equal(new Point2D(0, 100), p[(int)NodeHandle.SW]);
        Assert.Equal(new Point2D(0, 50), p[(int)NodeHandle.W]);
    }

    [Fact]
    public void HitTest_ReturnsHandleIndex_WhenWithinTolerance()
    {
        Assert.Equal((int)NodeHandle.NW, NodeHandleGeometry.HitTest(Box, new Point2D(2, 2), 5));
        Assert.Equal((int)NodeHandle.SE, NodeHandleGeometry.HitTest(Box, new Point2D(98, 98), 5));
    }

    [Fact]
    public void HitTest_ReturnsMinusOne_WhenOutsideAllHandles()
        => Assert.Equal(-1, NodeHandleGeometry.HitTest(Box, new Point2D(50, 50), 5));

    [Fact]
    public void HitTest_RespectsToleranceOnBothAxes()
    {
        Assert.Equal((int)NodeHandle.NW, NodeHandleGeometry.HitTest(Box, new Point2D(5, 5), 5));
        Assert.Equal(-1, NodeHandleGeometry.HitTest(Box, new Point2D(6, 0), 5)); // 6 > tolerance from NW
    }

    [Fact]
    public void Resize_CornerSE_GrowsToDraggedPoint()
        => Assert.Equal(new Rect2D(0, 0, 200, 200), NodeHandleGeometry.Resize(Box, (int)NodeHandle.SE, new Point2D(200, 200), 10, 10, false));

    [Fact]
    public void Resize_EdgeE_OnlyChangesWidth()
        => Assert.Equal(new Rect2D(0, 0, 200, 100), NodeHandleGeometry.Resize(Box, (int)NodeHandle.E, new Point2D(200, 999), 10, 10, false));

    [Fact]
    public void Resize_ClampsToMinimumSize()
        => Assert.Equal(new Rect2D(0, 0, 20, 20), NodeHandleGeometry.Resize(Box, (int)NodeHandle.SE, new Point2D(5, 5), 20, 20, false));

    [Fact]
    public void Resize_NormalizesWhenDraggedPastOppositeEdge()
        => Assert.Equal(new Rect2D(-30, 0, 30, 100), NodeHandleGeometry.Resize(Box, (int)NodeHandle.E, new Point2D(-30, 50), 10, 10, false));

    [Fact]
    public void Resize_LockAspect_Corner_PreservesRatio_FixesOppositeCorner()
    {
        // Non-square 2:1 box; drag SE — height is the dominant axis, NW corner stays fixed.
        Rect2D rect = new(0, 0, 100, 50);
        Assert.Equal(new Rect2D(0, 0, 400, 200), NodeHandleGeometry.Resize(rect, (int)NodeHandle.SE, new Point2D(200, 200), 10, 10, true));
    }

    [Fact]
    public void Resize_LockAspect_EdgeN_FixesBottomEdge_AndRecenters()
    {
        Rect2D rect = new(0, 0, 100, 50);
        Rect2D result = NodeHandleGeometry.Resize(rect, (int)NodeHandle.N, new Point2D(50, -50), 10, 10, true);
        Assert.Equal(new Rect2D(-50, -50, 200, 100), result);
        Assert.Equal(50d, result.Bottom);    // bottom edge unchanged
        Assert.Equal(50d, result.Center.X);  // horizontally re-centred on the original centre
    }

    [Fact]
    public void Resize_LockAspect_RespectsMinViaScaleFloor()
    {
        // Dragging far inward would shrink below the minimum; the scale floor holds the size up.
        Rect2D rect = new(0, 0, 100, 50);
        Assert.Equal(new Rect2D(0, 0, 80, 40), NodeHandleGeometry.Resize(rect, (int)NodeHandle.SE, new Point2D(5, 2), 40, 40, true));
    }
}
