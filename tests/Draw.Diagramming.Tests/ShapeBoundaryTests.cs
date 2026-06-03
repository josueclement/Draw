using Draw.Diagramming.Geometry;
using Draw.Model.Nodes;
using Draw.Model.Primitives;
using Xunit;

namespace Draw.Diagramming.Tests;

public class ShapeBoundaryTests
{
    private static readonly Rect2D Unit = new(0, 0, 100, 100);

    private static void AssertPoint(double x, double y, Point2D actual)
    {
        Assert.Equal(x, actual.X, 3);
        Assert.Equal(y, actual.Y, 3);
    }

    [Fact]
    public void Rectangle_ExitsRightEdge_WhenTargetIsToTheRight()
        => AssertPoint(100, 50, ShapeBoundary.IntersectFromCenter(ShapeKind.Rectangle, Unit, new Point2D(500, 50)));

    [Fact]
    public void Rectangle_ExitsBottomEdge_WhenTargetIsBelow()
        => AssertPoint(50, 100, ShapeBoundary.IntersectFromCenter(ShapeKind.Rectangle, Unit, new Point2D(50, 500)));

    [Fact]
    public void Ellipse_ExitsAtRightmostPoint_ForHorizontalRay()
        => AssertPoint(100, 50, ShapeBoundary.IntersectFromCenter(ShapeKind.Ellipse, Unit, new Point2D(500, 50)));

    [Fact]
    public void Diamond_ExitsAtRightVertex_ForHorizontalRay()
        => AssertPoint(100, 50, ShapeBoundary.IntersectFromCenter(ShapeKind.Diamond, Unit, new Point2D(500, 50)));

    [Fact]
    public void Triangle_ExitsBottomEdge_ForDownwardRay()
        => AssertPoint(50, 100, ShapeBoundary.IntersectFromCenter(ShapeKind.Triangle, Unit, new Point2D(50, 500)));

    [Fact]
    public void Circle_UsesInscribedCircle_ForWideBounds()
    {
        // 200x100 bounds => circle of diameter 100 centered at (100,50); exits at (150,50).
        Rect2D wide = new(0, 0, 200, 100);
        AssertPoint(150, 50, ShapeBoundary.IntersectFromCenter(ShapeKind.Circle, wide, new Point2D(500, 50)));
    }

    [Fact]
    public void DegenerateDirection_ReturnsCenter()
        => AssertPoint(50, 50, ShapeBoundary.IntersectFromCenter(ShapeKind.Rectangle, Unit, new Point2D(50, 50)));
}
