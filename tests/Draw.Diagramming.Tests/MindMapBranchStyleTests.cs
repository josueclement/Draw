using Draw.Diagramming.MindMap;
using Xunit;

namespace Draw.Diagramming.Tests;

public class MindMapBranchStyleTests
{
    [Fact]
    public void WidthAt_Depth0_IsBaseWidth()
        => Assert.Equal(MindMapBranchStyle.BaseWidth, MindMapBranchStyle.WidthAt(0), 6);

    [Fact]
    public void WidthAt_DecreasesWithDepth_UntilFloor()
    {
        double d0 = MindMapBranchStyle.WidthAt(0);
        double d1 = MindMapBranchStyle.WidthAt(1);
        double d2 = MindMapBranchStyle.WidthAt(2);
        Assert.True(d1 < d0);
        Assert.True(d2 < d1);
    }

    [Fact]
    public void WidthAt_FloorsAtMinWidth_ForDeepLevels()
        => Assert.Equal(MindMapBranchStyle.MinWidth, MindMapBranchStyle.WidthAt(100), 6);

    [Fact]
    public void WidthAt_NeverBelowMinWidth()
    {
        for (int depth = 0; depth <= 50; depth++)
        {
            Assert.True(MindMapBranchStyle.WidthAt(depth) >= MindMapBranchStyle.MinWidth);
        }
    }

    [Fact]
    public void WidthAt_NegativeDepth_ClampsToDepth0()
        => Assert.Equal(MindMapBranchStyle.WidthAt(0), MindMapBranchStyle.WidthAt(-5), 6);
}
