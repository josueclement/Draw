using System;
using System.Collections.Generic;
using Draw.Diagramming.Geometry;
using Draw.Model.Primitives;
using Xunit;

namespace Draw.Diagramming.Tests;

public class ConnectorHandleHitTesterTests
{
    private static readonly Point2D Start = new(0, 0);
    private static readonly Point2D End = new(100, 0);
    private const double Tolerance = 5d;

    private static ConnectorHandleHit Hit(
        Point2D world,
        IReadOnlyList<Point2D>? waypoints = null,
        ConnectorLabelBox center = default,
        ConnectorLabelBox source = default,
        ConnectorLabelBox target = default)
        => ConnectorHandleHitTester.Hit(Start, End, waypoints ?? Array.Empty<Point2D>(), center, source, target, world, Tolerance);

    [Fact]
    public void Hit_SourceEndpoint_WhenNearRouteStart()
        => Assert.Equal(ConnectorHandlePart.SourceEndpoint, Hit(new Point2D(2, 2)).Part);

    [Fact]
    public void Hit_TargetEndpoint_WhenNearRouteEnd()
        => Assert.Equal(ConnectorHandlePart.TargetEndpoint, Hit(new Point2D(101, 1)).Part);

    [Fact]
    public void Hit_None_WhenFarFromEverything()
    {
        ConnectorHandleHit hit = Hit(new Point2D(50, 50));
        Assert.Equal(ConnectorHandlePart.None, hit.Part);
        Assert.Equal(-1, hit.WaypointIndex);
    }

    [Fact]
    public void Hit_Waypoint_ReturnsIndex()
    {
        Point2D[] waypoints = { new(30, 0), new(60, 10) };
        ConnectorHandleHit hit = Hit(new Point2D(61, 11), waypoints);
        Assert.Equal(ConnectorHandlePart.Waypoint, hit.Part);
        Assert.Equal(1, hit.WaypointIndex);
    }

    [Fact]
    public void Hit_EndpointBeatsCoincidentWaypoint()
    {
        // A waypoint sitting on the source endpoint must not steal the endpoint grab.
        Point2D[] waypoints = { Start };
        Assert.Equal(ConnectorHandlePart.SourceEndpoint, Hit(new Point2D(0, 0), waypoints).Part);
    }

    [Fact]
    public void Hit_JustOutsideTolerance_IsNone()
        => Assert.Equal(ConnectorHandlePart.None, Hit(new Point2D(6, 0)).Part); // 6 > tolerance 5 from Start

    [Fact]
    public void Hit_CenterLabel_WhenInsideBoxAndAwayFromHandles()
    {
        ConnectorLabelBox center = new(true, new Point2D(40, 40), 3);
        Assert.Equal(ConnectorHandlePart.CenterLabel, Hit(new Point2D(45, 45), center: center).Part);
    }

    [Fact]
    public void Hit_LabelIsLowestPriority_EndpointWins()
    {
        // A label box covering the source endpoint still yields the endpoint.
        ConnectorLabelBox center = new(true, new Point2D(-10, -10), 10);
        Assert.Equal(ConnectorHandlePart.SourceEndpoint, Hit(new Point2D(0, 0), center: center).Part);
    }

    [Fact]
    public void Hit_CenterLabelBeatsSourceAndTarget_WhenOverlapping()
    {
        Point2D origin = new(40, 40);
        ConnectorLabelBox center = new(true, origin, 3);
        ConnectorLabelBox source = new(true, origin, 3);
        ConnectorLabelBox target = new(true, origin, 3);
        Assert.Equal(ConnectorHandlePart.CenterLabel, Hit(new Point2D(42, 42), center: center, source: source, target: target).Part);
    }

    [Fact]
    public void Hit_SkipsAbsentLabel()
    {
        // Centre absent, source present and hit.
        ConnectorLabelBox source = new(true, new Point2D(40, 40), 3);
        Assert.Equal(ConnectorHandlePart.SourceLabel, Hit(new Point2D(42, 42), source: source).Part);
    }

    [Fact]
    public void HitsLabel_WidthGrowsWithTextLength()
    {
        // 5 chars * 7px = 35px wide; box X spans [-2, 37], Y spans [-2, 18].
        ConnectorLabelBox label = new(true, new Point2D(0, 0), 5);
        Assert.True(ConnectorHandleHitTester.HitsLabel(label, new Point2D(33, 8)));
        Assert.False(ConnectorHandleHitTester.HitsLabel(label, new Point2D(80, 8)));
    }

    [Fact]
    public void Within_RespectsToleranceOnBothAxes()
    {
        Assert.True(ConnectorHandleHitTester.Within(new Point2D(0, 0), new Point2D(5, 5), 5));
        Assert.False(ConnectorHandleHitTester.Within(new Point2D(0, 0), new Point2D(6, 0), 5));
    }
}
