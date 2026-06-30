using System;
using System.Collections.Generic;
using Draw.Diagramming.Layout;
using Draw.Model.Primitives;
using Xunit;

namespace Draw.Diagramming.Tests;

public class ConnectionDistributorTests
{
    private static readonly Rect2D Box = new(0, 0, 100, 100);

    [Theory]
    [InlineData(0, 50, BoxSide.Left)]
    [InlineData(100, 50, BoxSide.Right)]
    [InlineData(50, 0, BoxSide.Top)]
    [InlineData(50, 100, BoxSide.Bottom)]
    public void ClassifySide_PicksNearestEdge(double x, double y, BoxSide expected)
    {
        Assert.Equal(expected, ConnectionDistributor.ClassifySide(Box, new Point2D(x, y)));
    }

    [Fact]
    public void ClassifySide_CenterTie_ResolvesToLeft()
    {
        Assert.Equal(BoxSide.Left, ConnectionDistributor.ClassifySide(Box, new Point2D(50, 50)));
    }

    [Fact]
    public void ClassifySide_DegenerateBounds_FallBackToLeft()
    {
        Assert.Equal(BoxSide.Left, ConnectionDistributor.ClassifySide(new Rect2D(0, 0, 0, 0), new Point2D(0, 0)));
    }

    [Theory]
    [InlineData(BoxSide.Left, 0, 25, 0.25)]
    [InlineData(BoxSide.Right, 0, 75, 0.75)]
    [InlineData(BoxSide.Top, 40, 0, 0.40)]
    [InlineData(BoxSide.Bottom, 90, 0, 0.90)]
    public void FractionAlong_MeasuresVaryingCoordinate(BoxSide side, double x, double y, double expected)
    {
        Assert.Equal(expected, ConnectionDistributor.FractionAlong(side, Box, new Point2D(x, y)), 3);
    }

    [Fact]
    public void EvenAnchor_SpacesPointsWithEqualGaps()
    {
        Assert.Equal(new Point2D(0, 0.5), ConnectionDistributor.EvenAnchor(BoxSide.Left, 0, 1));
        Assert.Equal(new Point2D(0, 0.25), ConnectionDistributor.EvenAnchor(BoxSide.Left, 0, 3));
        Assert.Equal(new Point2D(0.5, 0), ConnectionDistributor.EvenAnchor(BoxSide.Top, 1, 3));
        Assert.Equal(new Point2D(1, 0.5), ConnectionDistributor.EvenAnchor(BoxSide.Right, 0, 1));
    }

    [Fact]
    public void PlanPinning_OrdersEndsByFarShapePosition_NotCurrentPosition()
    {
        Guid node = Guid.NewGuid();
        // Three ends on node's LEFT edge. Their CURRENT positions run top->bottom (10,50,90), but the
        // shapes at their far ends run the OPPOSITE way (token 1's far shape is lowest, token 3's highest).
        // Spacing must follow the far shapes -> the slots reorder to 3,2,1 so the connectors don't cross.
        List<ConnectionDistributor.PinningEnd<int>> ends = new()
        {
            End(1, node, routePoint: new Point2D(0, 10), otherEnd: new Point2D(-100, 90)),
            End(2, node, routePoint: new Point2D(0, 50), otherEnd: new Point2D(-100, 50)),
            End(3, node, routePoint: new Point2D(0, 90), otherEnd: new Point2D(-100, 10)),
        };

        IReadOnlyList<(int Token, Point2D Anchor)> plan =
            ConnectionDistributor.PlanPinning(ends, ConnectionDistributor.EvenAnchor);

        // Sorted by far-shape Y ascending (10,50,90 -> tokens 3,2,1) and evenly spaced down the left edge.
        Assert.Equal(3, plan.Count);
        Assert.Equal((3, new Point2D(0, 0.25)), plan[0]);
        Assert.Equal((2, new Point2D(0, 0.5)), plan[1]);
        Assert.Equal((1, new Point2D(0, 0.75)), plan[2]);
    }

    [Fact]
    public void PlanPinning_EqualFarShapePosition_KeepsCurrentOrder()
    {
        Guid node = Guid.NewGuid();
        // Two ends to the SAME far position (tie on the order key): fall back to current order along the
        // edge (token 1 currently above token 2), so token 1 keeps the top slot.
        List<ConnectionDistributor.PinningEnd<int>> ends = new()
        {
            End(2, node, routePoint: new Point2D(0, 70), otherEnd: new Point2D(-100, 50)),
            End(1, node, routePoint: new Point2D(0, 30), otherEnd: new Point2D(-100, 50)),
        };

        IReadOnlyList<(int Token, Point2D Anchor)> plan =
            ConnectionDistributor.PlanPinning(ends, ConnectionDistributor.EvenAnchor);

        Assert.Equal(2, plan.Count);
        Assert.Equal((1, new Point2D(0, 1d / 3d)), plan[0]);
        Assert.Equal((2, new Point2D(0, 2d / 3d)), plan[1]);
    }

    [Fact]
    public void PlanPinning_OmitsEndAlreadyAtComputedAnchor()
    {
        Guid node = Guid.NewGuid();
        // A lone end on the left edge resolves to EvenAnchor(Left,0,1) = (0,0.5); it already sits there.
        List<ConnectionDistributor.PinningEnd<int>> ends = new()
        {
            new ConnectionDistributor.PinningEnd<int>(1, node, Box, new Point2D(0, 50), new Point2D(-100, 50), new Point2D(0, 0.5)),
        };

        IReadOnlyList<(int Token, Point2D Anchor)> plan =
            ConnectionDistributor.PlanPinning(ends, ConnectionDistributor.EvenAnchor);

        Assert.Empty(plan); // no change -> no-op
    }

    [Fact]
    public void PlanPinning_OmitsEndWithinToleranceOfComputedAnchor()
    {
        Guid node = Guid.NewGuid();
        // The computed anchor is (0,0.5); an end a hair off (sub-tolerance) still counts as "already
        // there" — guards the tolerance compare against the prior exact float == (which would re-pin).
        List<ConnectionDistributor.PinningEnd<int>> ends = new()
        {
            new ConnectionDistributor.PinningEnd<int>(1, node, Box, new Point2D(0, 50), new Point2D(-100, 50), new Point2D(0, 0.5 + 1e-12)),
        };

        IReadOnlyList<(int Token, Point2D Anchor)> plan =
            ConnectionDistributor.PlanPinning(ends, ConnectionDistributor.EvenAnchor);

        Assert.Empty(plan);
    }

    [Fact]
    public void PlanPinning_GroupsByNodeAndSide()
    {
        Guid a = Guid.NewGuid();
        Guid b = Guid.NewGuid();
        List<ConnectionDistributor.PinningEnd<int>> ends = new()
        {
            End(1, a, new Point2D(0, 50), new Point2D(-100, 50)), // node a, left edge
            End(2, b, new Point2D(0, 50), new Point2D(-100, 50)), // node b, left edge -> a separate group
        };

        IReadOnlyList<(int Token, Point2D Anchor)> plan =
            ConnectionDistributor.PlanPinning(ends, ConnectionDistributor.EvenAnchor);

        // Each is alone on its own node's side, so both centre at (0,0.5).
        Assert.Equal(2, plan.Count);
        Assert.All(plan, op => Assert.Equal(new Point2D(0, 0.5), op.Anchor));
    }

    private static ConnectionDistributor.PinningEnd<int> End(int token, Guid node, Point2D routePoint, Point2D otherEnd) =>
        new(token, node, Box, routePoint, otherEnd, null);
}
