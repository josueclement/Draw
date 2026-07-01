using System.Collections.Generic;
using Draw.Diagramming.MindMap;
using Draw.Model.Styling;
using Xunit;

namespace Draw.Diagramming.Tests;

public class BranchDashPatternTests
{
    [Fact]
    public void Solid_ReturnsEmpty()
        => Assert.Empty(BranchDashPattern.For(DashStyle.Solid, 9d));

    [Fact]
    public void NonPositiveWidth_ReturnsEmpty()
        => Assert.Empty(BranchDashPattern.For(DashStyle.Dash, 0d));

    [Theory]
    [InlineData(DashStyle.Dash, 2)]
    [InlineData(DashStyle.Dot, 2)]
    [InlineData(DashStyle.DashDot, 4)]
    public void NonSolid_ReturnsRunLengths(DashStyle dash, int expectedCount)
        => Assert.Equal(expectedCount, BranchDashPattern.For(dash, 9d).Count);

    [Fact]
    public void Dash_ScalesRatiosByWidthUnit()
    {
        // Dash ratio 4:2 × width unit 9 = 36 on, 18 off.
        IReadOnlyList<double> pattern = BranchDashPattern.For(DashStyle.Dash, 9d);
        Assert.Equal(36d, pattern[0], 6);
        Assert.Equal(18d, pattern[1], 6);
    }

    [Fact]
    public void ThinBranch_IsFlooredAtMinUnit()
    {
        // A width below MinUnit is floored so deep twigs keep readable dashes.
        IReadOnlyList<double> pattern = BranchDashPattern.For(DashStyle.Dash, 1d);
        Assert.Equal(4d * BranchDashPattern.MinUnit, pattern[0], 6);
        Assert.Equal(2d * BranchDashPattern.MinUnit, pattern[1], 6);
    }
}
