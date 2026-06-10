using System.Collections.Generic;
using System.Linq;
using Draw.Diagramming.Layout;
using Xunit;

namespace Draw.Diagramming.Tests;

public class ZOrderArrangerTests
{
    private static readonly int[] BackToFront = { 0, 1, 2, 3 };

    [Fact]
    public void Reorder_BringToFront_MovesSelectedToEnd()
    {
        IReadOnlyList<int> result = ZOrderArranger.Reorder(BackToFront, i => i == 1, ZOrderOperation.BringToFront);
        Assert.Equal(new[] { 0, 2, 3, 1 }, result);
    }

    [Fact]
    public void Reorder_SendToBack_MovesSelectedToStart()
    {
        IReadOnlyList<int> result = ZOrderArranger.Reorder(BackToFront, i => i == 2, ZOrderOperation.SendToBack);
        Assert.Equal(new[] { 2, 0, 1, 3 }, result);
    }

    [Fact]
    public void Reorder_BringForward_MovesSelectedOneLevelTowardFront()
    {
        IReadOnlyList<int> result = ZOrderArranger.Reorder(BackToFront, i => i == 1, ZOrderOperation.BringForward);
        Assert.Equal(new[] { 0, 2, 1, 3 }, result);
    }

    [Fact]
    public void Reorder_SendBackward_MovesSelectedOneLevelTowardBack()
    {
        IReadOnlyList<int> result = ZOrderArranger.Reorder(BackToFront, i => i == 2, ZOrderOperation.SendBackward);
        Assert.Equal(new[] { 0, 2, 1, 3 }, result);
    }

    [Fact]
    public void Reorder_ContiguousBlock_MovesAsOneUnit()
    {
        IReadOnlyList<int> result = ZOrderArranger.Reorder(BackToFront, i => i is 1 or 2, ZOrderOperation.BringForward);
        Assert.Equal(new[] { 0, 3, 1, 2 }, result);
    }

    [Theory]
    [InlineData(ZOrderOperation.BringToFront)]
    [InlineData(ZOrderOperation.SendToBack)]
    [InlineData(ZOrderOperation.BringForward)]
    [InlineData(ZOrderOperation.SendBackward)]
    public void Reorder_NoOp_WhenNoneOrAllSelected(ZOrderOperation op)
    {
        Assert.Equal(BackToFront, ZOrderArranger.Reorder(BackToFront, _ => false, op));
        Assert.Equal(BackToFront, ZOrderArranger.Reorder(BackToFront, _ => true, op));
    }

    private readonly record struct Item(string Id, bool IsBoundary, int Z);

    [Fact]
    public void ReorderInBands_KeepsLowerBandBelow_EvenWhenBroughtToFront()
    {
        Item[] items = { new("ord", false, 0), new("bnd", true, 1) };

        IReadOnlyList<Item> result = ZOrderArranger.ReorderInBands(
            items, i => i.IsBoundary, i => i.Id == "bnd", i => i.Z, ZOrderOperation.BringToFront);

        // The boundary is "brought to front" but stays confined to the lower band, behind the shape.
        Assert.Equal("bnd", result[0].Id);
        Assert.Equal("ord", result[1].Id);
    }

    [Fact]
    public void ReorderInBands_ReordersWithinUpperBand()
    {
        Item[] items = { new("a", false, 0), new("c", false, 2), new("bnd", true, 1) };

        IReadOnlyList<Item> result = ZOrderArranger.ReorderInBands(
            items, i => i.IsBoundary, i => i.Id == "a", i => i.Z, ZOrderOperation.BringToFront);

        // Lower band (bnd) first, then the ordinary band with 'a' brought to the front.
        Assert.Equal(new[] { "bnd", "c", "a" }, result.Select(i => i.Id));
    }
}
