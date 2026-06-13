using Draw.Model.Primitives;
using Xunit;

namespace Draw.Model.Tests;

public class Point2DTests
{
    [Fact]
    public void Normalized_ZeroVector_ReturnsOrigin()
        => Assert.Equal(Point2D.Origin, Point2D.Origin.Normalized());

    [Fact]
    public void Normalized_LengthBelowTolerance_ReturnsOrigin()
        => Assert.Equal(Point2D.Origin, new Point2D(1e-12, 0).Normalized()); // > double.Epsilon, but below the 1e-9 guard

    [Fact]
    public void Normalized_UnitVector_ReturnsItself()
        => Assert.Equal(new Point2D(1, 0), new Point2D(1, 0).Normalized());

    [Fact]
    public void Normalized_ArbitraryVector_HasUnitLength()
    {
        Point2D unit = new Point2D(3, 4).Normalized();
        Assert.Equal(0.6d, unit.X, 9);
        Assert.Equal(0.8d, unit.Y, 9);
        Assert.Equal(1d, unit.Length, 9);
    }

    [Fact]
    public void ApproximatelyEquals_WithinTolerance_IsTrue()
        => Assert.True(new Point2D(1, 1).ApproximatelyEquals(new Point2D(1 + 1e-10, 1 - 1e-10), 1e-9));

    [Fact]
    public void ApproximatelyEquals_OutsideTolerance_IsFalse()
        => Assert.False(new Point2D(1, 1).ApproximatelyEquals(new Point2D(1.001, 1), 1e-9));
}
