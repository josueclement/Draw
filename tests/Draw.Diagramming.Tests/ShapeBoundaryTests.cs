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
    public void RoundedRectangle_AttachesAsPlainRectangle()
        => AssertPoint(100, 50, ShapeBoundary.IntersectFromCenter(ShapeKind.RoundedRectangle, Unit, new Point2D(500, 50)));

    [Fact]
    public void Note_AttachesAsPlainRectangle()
        => AssertPoint(100, 50, ShapeBoundary.IntersectFromCenter(ShapeKind.Note, Unit, new Point2D(500, 50)));

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
    public void Parallelogram_ExitsRightSlantedEdge_ForHorizontalRay()
        => AssertPoint(87.5, 50, ShapeBoundary.IntersectFromCenter(ShapeKind.Parallelogram, Unit, new Point2D(500, 50)));

    [Fact]
    public void Trapezoid_ExitsRightSlantedEdge_ForHorizontalRay()
        => AssertPoint(90, 50, ShapeBoundary.IntersectFromCenter(ShapeKind.Trapezoid, Unit, new Point2D(500, 50)));

    [Fact]
    public void Circle_UsesInscribedCircle_ForWideBounds()
    {
        // 200x100 bounds => circle of diameter 100 centred at (100,50); exits at (150,50).
        Rect2D wide = new(0, 0, 200, 100);
        AssertPoint(150, 50, ShapeBoundary.IntersectFromCenter(ShapeKind.Circle, wide, new Point2D(500, 50)));
    }

    [Fact]
    public void Hexagon_ExitsAtRightVertex_ForHorizontalRay()
        => AssertPoint(100, 50, ShapeBoundary.IntersectFromCenter(ShapeKind.Hexagon, Unit, new Point2D(500, 50)));

    [Fact]
    public void Pentagon_ExitsAtApex_ForUpwardRay()
        => AssertPoint(50, 0, ShapeBoundary.IntersectFromCenter(ShapeKind.Pentagon, Unit, new Point2D(50, -500)));

    [Fact]
    public void Octagon_ExitsAtRightFlatEdge_ForHorizontalRay()
        // Flat-top octagon (start 22.5°): the right edge sits at x = 50 + 50·cos 22.5° ≈ 96.194.
        => AssertPoint(96.194, 50, ShapeBoundary.IntersectFromCenter(ShapeKind.Octagon, Unit, new Point2D(500, 50)));

    [Fact]
    public void Star_ExitsAtApex_ForUpwardRay()
        => AssertPoint(50, 0, ShapeBoundary.IntersectFromCenter(ShapeKind.Star, Unit, new Point2D(50, -500)));

    [Fact]
    public void Cross_ExitsAtRightArmEdge_ForHorizontalRay()
        => AssertPoint(100, 50, ShapeBoundary.IntersectFromCenter(ShapeKind.Cross, Unit, new Point2D(500, 50)));

    [Fact]
    public void Callout_ExitsAtBodyRightEdge_ForHorizontalRay()
        => AssertPoint(100, 50, ShapeBoundary.IntersectFromCenter(ShapeKind.Callout, Unit, new Point2D(500, 50)));

    [Fact]
    public void Cloud_AttachesAsBoundingRectangle_ForHorizontalRay()
        => AssertPoint(100, 50, ShapeBoundary.IntersectFromCenter(ShapeKind.Cloud, Unit, new Point2D(500, 50)));

    [Fact]
    public void DegenerateDirection_ReturnsCenter()
        => AssertPoint(50, 50, ShapeBoundary.IntersectFromCenter(ShapeKind.Rectangle, Unit, new Point2D(50, 50)));

    [Fact]
    public void ParallelRay_SkipsCollinearEdge_AndHitsTheCrossingEdge()
    {
        // A ray along (1,1) from the diamond centre is parallel to the top-right edge (50,0)->(100,50),
        // so that edge is skipped; the ray crosses the lower-right edge at its midpoint (75,75).
        AssertPoint(75, 75, ShapeBoundary.IntersectFromCenter(ShapeKind.Diamond, Unit, new Point2D(150, 150)));
    }

    [Fact]
    public void DegenerateEllipse_ZeroHeight_ReturnsCenter()
    {
        Rect2D flat = new(0, 0, 100, 0);
        AssertPoint(50, 0, ShapeBoundary.IntersectFromCenter(ShapeKind.Ellipse, flat, new Point2D(500, 50)));
    }

    [Fact]
    public void DegenerateBounds_ZeroSize_ReturnsCenter()
    {
        Rect2D zero = new(0, 0, 0, 0);
        AssertPoint(0, 0, ShapeBoundary.IntersectFromCenter(ShapeKind.Rectangle, zero, new Point2D(500, 50)));
    }

    [Fact]
    public void ResolveAnchor_RightMidpoint_LandsOnRightEdge()
        => AssertPoint(100, 50, ShapeBoundary.ResolveAnchor(ShapeKind.Rectangle, Unit, new Point2D(1, 0.5)));

    [Fact]
    public void ResolveAnchor_TopMidpoint_LandsOnTopEdge()
        => AssertPoint(50, 0, ShapeBoundary.ResolveAnchor(ShapeKind.Rectangle, Unit, new Point2D(0.5, 0)));

    [Fact]
    public void ResolveAnchor_Center_ReturnsCenter()
        => AssertPoint(50, 50, ShapeBoundary.ResolveAnchor(ShapeKind.Rectangle, Unit, new Point2D(0.5, 0.5)));
}
