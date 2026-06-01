using Jcl.Draw.Model.Primitives;
using Xunit;

namespace Jcl.Draw.Model.Tests;

public class Rect2DTests
{
    [Fact]
    public void FromPoints_OrdersCorners()
    {
        Rect2D rect = Rect2D.FromPoints(new Point2D(30, 40), new Point2D(10, 20));

        Assert.Equal(new Rect2D(10, 20, 20, 20), rect);
    }

    [Fact]
    public void Normalized_FlipsNegativeExtents()
    {
        Rect2D rect = new(10, 10, -4, -6);

        Assert.Equal(new Rect2D(6, 4, 4, 6), rect.Normalized());
    }

    [Fact]
    public void Contains_PointInsideAndOutside()
    {
        Rect2D rect = new(0, 0, 10, 10);

        Assert.True(rect.Contains(new Point2D(5, 5)));
        Assert.False(rect.Contains(new Point2D(11, 5)));
    }

    [Fact]
    public void IntersectsWith_DetectsOverlapAndGap()
    {
        Rect2D a = new(0, 0, 10, 10);

        Assert.True(a.IntersectsWith(new Rect2D(5, 5, 10, 10)));
        Assert.False(a.IntersectsWith(new Rect2D(20, 20, 5, 5)));
    }

    [Fact]
    public void Union_EnclosesBoth()
    {
        Rect2D union = new Rect2D(0, 0, 10, 10).Union(new Rect2D(20, 5, 10, 10));

        Assert.Equal(Rect2D.FromLtrb(0, 0, 30, 15), union);
    }

    [Fact]
    public void Center_IsMidpoint()
    {
        Assert.Equal(new Point2D(15, 25), new Rect2D(10, 20, 10, 10).Center);
    }
}
