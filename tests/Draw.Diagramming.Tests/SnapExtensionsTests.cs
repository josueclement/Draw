using Draw.Diagramming.Geometry;
using Draw.Model.Primitives;
using Xunit;

namespace Draw.Diagramming.Tests;

public class SnapExtensionsTests
{
    [Theory]
    [InlineData(12d, 10d, 10d)]
    [InlineData(14d, 10d, 10d)]
    [InlineData(15d, 10d, 20d)]   // .5 rounds away from zero
    [InlineData(-12d, 10d, -10d)]
    [InlineData(-15d, 10d, -20d)] // .5 rounds away from zero (negative)
    [InlineData(0d, 10d, 0d)]
    public void SnapValue_RoundsToNearestMultiple(double value, double gridSize, double expected)
        => Assert.Equal(expected, SnapExtensions.SnapValue(value, gridSize));

    [Theory]
    [InlineData(0d)]
    [InlineData(-5d)]
    public void SnapValue_NonPositiveGrid_ReturnsValueUnchanged(double gridSize)
        => Assert.Equal(12.34d, SnapExtensions.SnapValue(12.34d, gridSize));

    [Fact]
    public void SnappedToGrid_SnapsBothAxes()
        => Assert.Equal(new Point2D(10, 30), new Point2D(12, 27).SnappedToGrid(10));

    [Fact]
    public void PositionSnappedToGrid_SnapsTopLeft_PreservesSize()
    {
        Rect2D snapped = new Rect2D(12, 27, 33, 44).PositionSnappedToGrid(10);
        Assert.Equal(new Rect2D(10, 30, 33, 44), snapped);
    }

    [Fact]
    public void EdgesSnappedToGrid_SnapsAllFourEdges()
    {
        // Left 12→10, Top 27→30, Right 45→50, Bottom 68→70.
        Rect2D snapped = new Rect2D(12, 27, 33, 41).EdgesSnappedToGrid(10);
        Assert.Equal(Rect2D.FromLtrb(10, 30, 50, 70), snapped);
    }
}
