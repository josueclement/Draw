using Draw.Diagramming.Geometry;
using Draw.Model.Primitives;
using Xunit;

namespace Draw.Diagramming.Tests;

public class EndpointAnchorMathTests
{
    private static readonly Rect2D Bounds = new(10, 20, 100, 50);

    private static void AssertAnchor(double u, double v, Point2D actual)
    {
        Assert.Equal(u, actual.X, 6);
        Assert.Equal(v, actual.Y, 6);
    }

    [Fact]
    public void Center_MapsToHalfHalf()
        => AssertAnchor(0.5, 0.5, EndpointAnchorMath.RelativeAnchor(Bounds, Bounds.Center));

    [Fact]
    public void TopLeft_MapsToZero()
        => AssertAnchor(0, 0, EndpointAnchorMath.RelativeAnchor(Bounds, new Point2D(Bounds.Left, Bounds.Top)));

    [Fact]
    public void BottomRight_MapsToOne()
        => AssertAnchor(1, 1, EndpointAnchorMath.RelativeAnchor(Bounds, new Point2D(Bounds.Right, Bounds.Bottom)));

    [Fact]
    public void OutsidePoint_IsClampedToUnitSquare()
        => AssertAnchor(0, 1, EndpointAnchorMath.RelativeAnchor(Bounds, new Point2D(-1000, 1000)));

    [Fact]
    public void ZeroWidth_CollapsesUToCenter()
        => AssertAnchor(0.5, 0, EndpointAnchorMath.RelativeAnchor(new Rect2D(5, 5, 0, 100), new Point2D(5, 5)));

    [Fact]
    public void ZeroHeight_CollapsesVToCenter()
        => AssertAnchor(0, 0.5, EndpointAnchorMath.RelativeAnchor(new Rect2D(5, 5, 100, 0), new Point2D(5, 5)));
}
