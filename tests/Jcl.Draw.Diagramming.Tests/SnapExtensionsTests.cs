using Jcl.Draw.Diagramming.Geometry;
using Jcl.Draw.Model.Primitives;
using Xunit;

namespace Jcl.Draw.Diagramming.Tests;

public class SnapExtensionsTests
{
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(4, 10, 0)]
    [InlineData(5, 10, 10)]
    [InlineData(14, 10, 10)]
    [InlineData(-5, 10, -10)]
    public void SnapValue_RoundsToNearestGridMultiple(double value, double grid, double expected)
    {
        Assert.Equal(expected, SnapExtensions.SnapValue(value, grid), 3);
    }

    [Fact]
    public void SnapValue_NonPositiveGrid_PassesThrough()
    {
        Assert.Equal(7.3, SnapExtensions.SnapValue(7.3, 0), 3);
    }

    [Fact]
    public void Point_SnappedToGrid_SnapsBothAxes()
    {
        Point2D snapped = new Point2D(13, 27).SnappedToGrid(10);

        Assert.Equal(new Point2D(10, 30), snapped);
    }

    [Fact]
    public void Rect_PositionSnappedToGrid_PreservesSize()
    {
        Rect2D snapped = new Rect2D(13, 27, 44, 51).PositionSnappedToGrid(10);

        Assert.Equal(new Rect2D(10, 30, 44, 51), snapped);
    }

    [Fact]
    public void Rect_EdgesSnappedToGrid_SnapsAllEdges()
    {
        Rect2D snapped = new Rect2D(13, 27, 44, 48).EdgesSnappedToGrid(10);

        // left 13->10, top 27->30, right 57->60, bottom 75->80 => (10,30,50,50)
        Assert.Equal(Rect2D.FromLtrb(10, 30, 60, 80), snapped);
    }
}
