using System;
using System.Collections.Generic;
using Draw.Diagramming.Routing;
using Draw.Model.Connectors;
using Draw.Model.Nodes;
using Draw.Model.Primitives;
using Xunit;

namespace Draw.Diagramming.Tests;

public class ConnectorRoutingTests
{
    private static ConnectorRouter CreateRouter()
        => new(new IConnectorRouteStrategy[] { new StraightRouter(), new OrthogonalRouter(), new RoundedRouter() });

    private static ConnectorRouteRequest SideBySide(RouteStyle style, IReadOnlyList<Point2D>? bendPoints = null)
        => new(
            ShapeKind.Rectangle, new Rect2D(0, 0, 100, 100),
            ShapeKind.Rectangle, new Rect2D(200, 0, 100, 100),
            style, bendPoints);

    [Fact]
    public void Straight_AttachesToFacingEdges()
    {
        ConnectorRoute route = CreateRouter().Route(SideBySide(RouteStyle.Straight));

        Assert.Null(route.Cubics);
        Assert.Equal(100, route.Start.X, 3);
        Assert.Equal(50, route.Start.Y, 3);
        Assert.Equal(200, route.End.X, 3);
        Assert.Equal(50, route.End.Y, 3);
        Assert.Equal(1, route.StartDirection.X, 3);
        Assert.Equal(0, route.StartDirection.Y, 3);
    }

    [Fact]
    public void Straight_RoutesThroughBendPoints()
    {
        ConnectorRoute route = CreateRouter().Route(SideBySide(RouteStyle.Straight, new[] { new Point2D(150, 150) }));

        Assert.Contains(route.Points, p => p.DistanceTo(new Point2D(150, 150)) < 1e-6);
    }

    [Fact]
    public void Straight_ForcedSourceAnchor_OverridesAutomaticAttachment()
    {
        // Anchor (0, 0.5) is the left-edge midpoint; without it the source would attach on the
        // right edge facing the target.
        ConnectorRouteRequest request = new(
            ShapeKind.Rectangle, new Rect2D(0, 0, 100, 100),
            ShapeKind.Rectangle, new Rect2D(200, 0, 100, 100),
            RouteStyle.Straight, sourceAnchor: new Point2D(0, 0.5));

        ConnectorRoute route = CreateRouter().Route(request);

        Assert.Equal(0, route.Start.X, 3);
        Assert.Equal(50, route.Start.Y, 3);
    }

    [Fact]
    public void Orthogonal_ProducesAxisAlignedElbow_ForDiagonalLayout()
    {
        ConnectorRouteRequest request = new(
            ShapeKind.Rectangle, new Rect2D(0, 0, 100, 100),
            ShapeKind.Rectangle, new Rect2D(200, 200, 100, 100),
            RouteStyle.Orthogonal);

        ConnectorRoute route = CreateRouter().Route(request);

        Assert.Null(route.Cubics);
        AssertAllSegmentsAxisAligned(route);
        Assert.True(route.Points.Count >= 3);
    }

    [Fact]
    public void Orthogonal_OverlappingBounds_FallsBackToDirectSegment()
    {
        ConnectorRouteRequest request = new(
            ShapeKind.Rectangle, new Rect2D(0, 0, 100, 100),
            ShapeKind.Rectangle, new Rect2D(50, 50, 100, 100),
            RouteStyle.Orthogonal);

        ConnectorRoute route = CreateRouter().Route(request);

        Assert.Null(route.Cubics);
        Assert.Equal(2, route.Points.Count);
    }

    [Fact]
    public void Orthogonal_WithBendPoints_AllSegmentsAxisAligned()
    {
        ConnectorRoute route = CreateRouter().Route(SideBySide(RouteStyle.Orthogonal, new[] { new Point2D(150, 30) }));

        AssertAllSegmentsAxisAligned(route);
    }

    [Fact]
    public void Rounded_NoBendPoints_ProducesSingleCubicSegment()
    {
        ConnectorRoute route = CreateRouter().Route(SideBySide(RouteStyle.Rounded));

        IReadOnlyList<CubicSegment>? cubics = route.Cubics;
        Assert.NotNull(cubics);
        Assert.Single(cubics);
        Assert.Equal(100, route.Start.X, 3);
        Assert.Equal(200, route.End.X, 3);
    }

    [Theory]
    [InlineData(1, 1)] // 1 bend  -> 3 points -> 1 segment
    [InlineData(2, 2)] // 2 bends -> 4 points -> 2 segments
    public void Rounded_SegmentCount_MatchesPointCountMinusTwo(int bendCount, int expectedSegments)
    {
        List<Point2D> bends = new();
        if (bendCount == 1)
        {
            bends.Add(new Point2D(100, 150));
        }
        else
        {
            bends.Add(new Point2D(80, 150));
            bends.Add(new Point2D(120, 150));
        }

        ConnectorRoute route = CreateRouter().Route(SideBySide(RouteStyle.Rounded, bends));

        IReadOnlyList<CubicSegment>? cubics = route.Cubics;
        Assert.NotNull(cubics);
        Assert.Equal(expectedSegments, cubics.Count);
    }

    [Fact]
    public void Rounded_DegenerateSequence_ProducesSingleSegment()
    {
        // Coincident bounds + a bend at the shared centre: every attachment point collapses, so the
        // deduped sequence has fewer than three points and the router takes its degenerate path.
        ConnectorRouteRequest request = new(
            ShapeKind.Rectangle, new Rect2D(0, 0, 100, 100),
            ShapeKind.Rectangle, new Rect2D(0, 0, 100, 100),
            RouteStyle.Rounded, new[] { new Point2D(50, 50) });

        ConnectorRoute route = CreateRouter().Route(request);

        IReadOnlyList<CubicSegment>? cubics = route.Cubics;
        Assert.NotNull(cubics);
        Assert.Single(cubics);
    }

    [Fact]
    public void Router_FallsBackToStraight_ForUnregisteredStyle()
    {
        ConnectorRouter router = new(new IConnectorRouteStrategy[] { new StraightRouter() });

        ConnectorRoute route = router.Route(SideBySide(RouteStyle.Rounded));

        Assert.Null(route.Cubics);
    }

    [Theory]
    [InlineData(RouteStyle.Straight)]
    [InlineData(RouteStyle.Orthogonal)]
    [InlineData(RouteStyle.Rounded)]
    public void CoincidentNodes_ProduceNonZeroEndpointDirections(RouteStyle style)
    {
        ConnectorRouteRequest request = new(
            ShapeKind.Rectangle, new Rect2D(0, 0, 100, 100),
            ShapeKind.Rectangle, new Rect2D(0, 0, 100, 100),
            style);

        ConnectorRoute route = CreateRouter().Route(request);

        Assert.True(route.StartDirection.Length > 0.5, "StartDirection must be a non-zero unit vector.");
        Assert.True(route.EndDirection.Length > 0.5, "EndDirection must be a non-zero unit vector.");
    }

    [Fact]
    public void Polyline_EmptyInput_Throws()
        => Assert.Throws<ArgumentException>(() => ConnectorRoute.Polyline(Array.Empty<Point2D>()));

    [Fact]
    public void PolyCubic_EmptySegments_Throws()
        => Assert.Throws<ArgumentException>(() => ConnectorRoute.PolyCubic(Point2D.Origin, Array.Empty<CubicSegment>()));

    [Fact]
    public void Polyline_SinglePoint_DuplicatesWithNonZeroDirection()
    {
        ConnectorRoute route = ConnectorRoute.Polyline(new[] { new Point2D(5, 5) });

        Assert.Equal(2, route.Points.Count);
        Assert.Equal(new Point2D(5, 5), route.Start);
        Assert.Equal(new Point2D(5, 5), route.End);
        Assert.True(route.StartDirection.Length > 0.5);
        Assert.True(route.EndDirection.Length > 0.5);
    }

    private static void AssertAllSegmentsAxisAligned(ConnectorRoute route)
    {
        for (int i = 1; i < route.Points.Count; i++)
        {
            Point2D a = route.Points[i - 1];
            Point2D b = route.Points[i];
            bool axisAligned = Math.Abs(a.X - b.X) < 1e-6 || Math.Abs(a.Y - b.Y) < 1e-6;
            Assert.True(axisAligned, $"Segment {a}->{b} is not axis-aligned.");
        }
    }
}
