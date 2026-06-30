using System.Collections.Generic;
using Draw.Diagramming.Geometry;
using Draw.Model.Primitives;
using Xunit;

namespace Draw.Diagramming.Tests;

public class DirectionalNavigatorTests
{
    // A 10x10 box centred on (cx, cy).
    private static Rect2D Box(double cx, double cy) => new(cx - 5, cy - 5, 10, 10);

    [Fact]
    public void FindNearest_PicksTheClosestShapeInTheDirection()
    {
        IReadOnlyList<Rect2D> shapes =
        [
            Box(100, 100), // 0: far right
            Box(60, 100),  // 1: near right
            Box(0, 100),   // 2: left
        ];

        Assert.Equal(1, DirectionalNavigator.FindNearest(new Point2D(50, 100), shapes, MoveDirection.Right));
        Assert.Equal(2, DirectionalNavigator.FindNearest(new Point2D(50, 100), shapes, MoveDirection.Left));
    }

    [Fact]
    public void FindNearest_ExcludesShapesNotInTheDirection()
    {
        IReadOnlyList<Rect2D> shapes = [Box(0, 100)]; // only a shape to the left
        Assert.Null(DirectionalNavigator.FindNearest(new Point2D(50, 100), shapes, MoveDirection.Right));
    }

    [Fact]
    public void FindNearest_ExcludesShapesAtTheSameAxisPosition()
    {
        // Same X as the reference: not strictly to the left or right.
        IReadOnlyList<Rect2D> shapes = [Box(50, 200)];
        Assert.Null(DirectionalNavigator.FindNearest(new Point2D(50, 100), shapes, MoveDirection.Right));
        Assert.Null(DirectionalNavigator.FindNearest(new Point2D(50, 100), shapes, MoveDirection.Left));
        // ...but it is below.
        Assert.Equal(0, DirectionalNavigator.FindNearest(new Point2D(50, 100), shapes, MoveDirection.Down));
    }

    [Fact]
    public void FindNearest_FavoursAlignedShapeOverACloserButOffAxisOne()
    {
        IReadOnlyList<Rect2D> shapes =
        [
            Box(70, 180), // 0: nearer in X (along=20) but far off-axis (perp=80) -> score 20 + 2*80 = 180
            Box(95, 100), // 1: aligned (perp=0), along=45 -> score 45
        ];

        Assert.Equal(1, DirectionalNavigator.FindNearest(new Point2D(50, 100), shapes, MoveDirection.Right));
    }

    [Fact]
    public void FindNearest_HandlesVerticalDirections()
    {
        IReadOnlyList<Rect2D> shapes =
        [
            Box(100, 40),  // 0: up (and right)
            Box(100, 160), // 1: down
        ];

        Assert.Equal(0, DirectionalNavigator.FindNearest(new Point2D(100, 100), shapes, MoveDirection.Up));
        Assert.Equal(1, DirectionalNavigator.FindNearest(new Point2D(100, 100), shapes, MoveDirection.Down));
    }

    [Fact]
    public void FindNearest_ReturnsNull_ForEmptyCandidates()
        => Assert.Null(DirectionalNavigator.FindNearest(Point2D.Origin, [], MoveDirection.Right));
}
