using Jcl.Draw.Diagramming.Routing;
using Jcl.Draw.Model.Connectors;
using Jcl.Draw.Model.Nodes;
using Jcl.Draw.Model.Primitives;
using Xunit;

namespace Jcl.Draw.Diagramming.Tests;

public class ConnectorRoutingTests
{
    private static ConnectorRouter CreateRouter()
        => new(new IConnectorRouteStrategy[] { new StraightRouter(), new OrthogonalRouter(), new BezierRouter() });

    private static ConnectorRouteRequest SideBySide(RouteStyle style)
        => new(
            ShapeKind.Rectangle, new Rect2D(0, 0, 100, 100),
            ShapeKind.Rectangle, new Rect2D(200, 0, 100, 100),
            style);

    [Fact]
    public void Straight_AttachesToFacingEdges()
    {
        ConnectorRoute route = CreateRouter().Route(SideBySide(RouteStyle.Straight));

        Assert.False(route.IsBezier);
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
        ConnectorRouteRequest request = new(
            ShapeKind.Rectangle, new Rect2D(0, 0, 100, 100),
            ShapeKind.Rectangle, new Rect2D(200, 0, 100, 100),
            RouteStyle.Straight,
            new[] { new Point2D(150, 150) });

        ConnectorRoute route = CreateRouter().Route(request);

        Assert.Contains(route.Points, p => p.DistanceTo(new Point2D(150, 150)) < 1e-6);
    }

    [Fact]
    public void Bezier_ProducesCubicWithControlPoints()
    {
        ConnectorRoute route = CreateRouter().Route(SideBySide(RouteStyle.Bezier));

        Assert.True(route.IsBezier);
        Assert.Equal(100, route.Start.X, 3);
        Assert.Equal(200, route.End.X, 3);
        Assert.True(route.Control1.X > route.Start.X);
        Assert.True(route.Control2.X < route.End.X);
    }

    [Fact]
    public void Orthogonal_ProducesAxisAlignedElbow_ForDiagonalLayout()
    {
        ConnectorRouteRequest request = new(
            ShapeKind.Rectangle, new Rect2D(0, 0, 100, 100),
            ShapeKind.Rectangle, new Rect2D(200, 200, 100, 100),
            RouteStyle.Orthogonal);

        ConnectorRoute route = CreateRouter().Route(request);

        // Every segment must be axis-aligned (horizontal or vertical).
        for (int i = 1; i < route.Points.Count; i++)
        {
            Point2D a = route.Points[i - 1];
            Point2D b = route.Points[i];
            bool axisAligned = System.Math.Abs(a.X - b.X) < 1e-6 || System.Math.Abs(a.Y - b.Y) < 1e-6;
            Assert.True(axisAligned, $"Segment {a}->{b} is not axis-aligned.");
        }

        Assert.True(route.Points.Count >= 3);
    }

    [Fact]
    public void Router_FallsBackToStraight_ForUnregisteredStyle()
    {
        ConnectorRouter router = new(new IConnectorRouteStrategy[] { new StraightRouter() });

        ConnectorRoute route = router.Route(SideBySide(RouteStyle.Bezier));

        Assert.False(route.IsBezier);
    }
}
