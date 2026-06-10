using System;
using System.Collections.Generic;
using Draw.Model.Primitives;

namespace Draw.Diagramming.Geometry;

/// <summary>Which interactive part of a selected connector a hit-test landed on.</summary>
public enum ConnectorHandlePart
{
    None,
    SourceEndpoint,
    TargetEndpoint,
    Waypoint,
    SourceLabel,
    CenterLabel,
    TargetLabel,
}

/// <summary>
/// Result of a connector handle hit-test. <see cref="WaypointIndex"/> is meaningful only when
/// <see cref="Part"/> is <see cref="ConnectorHandlePart.Waypoint"/> (otherwise -1).
/// </summary>
public readonly record struct ConnectorHandleHit(ConnectorHandlePart Part, int WaypointIndex)
{
    public static ConnectorHandleHit None => new(ConnectorHandlePart.None, -1);
}

/// <summary>
/// A connector label's hit box: whether it is shown, its top-left in world coordinates, and the
/// length of its text (the width is estimated from the character count).
/// </summary>
public readonly record struct ConnectorLabelBox(bool Present, Point2D TopLeft, int TextLength);

/// <summary>
/// Framework-agnostic hit-testing for the editable handles of a selected connector. Endpoints and
/// bend points use the precise handle tolerance; labels use an estimated text box and are lowest
/// priority so they never steal an endpoint grab. The connector layer itself is not hit-testable,
/// so this code-based test is what selects a handle under the cursor.
/// </summary>
public static class ConnectorHandleHitTester
{
    // Label box estimate: width grows with text length (FontSize 11 ≈ 7px/char), 16px tall, 2px slack.
    private const double MinLabelWidth = 8d;
    private const double CharWidthEstimate = 7d;
    private const double LabelHeight = 16d;
    private const double LabelSlack = 2d;

    /// <summary>
    /// Returns the front-most handle under <paramref name="world"/>, in priority order: source
    /// endpoint, target endpoint, bend points (by index), then the centre/source/target labels.
    /// </summary>
    public static ConnectorHandleHit Hit(
        Point2D routeStart,
        Point2D routeEnd,
        IReadOnlyList<Point2D> waypoints,
        ConnectorLabelBox centerLabel,
        ConnectorLabelBox sourceLabel,
        ConnectorLabelBox targetLabel,
        Point2D world,
        double tolerance)
    {
        if (Within(routeStart, world, tolerance))
        {
            return new ConnectorHandleHit(ConnectorHandlePart.SourceEndpoint, -1);
        }

        if (Within(routeEnd, world, tolerance))
        {
            return new ConnectorHandleHit(ConnectorHandlePart.TargetEndpoint, -1);
        }

        for (int i = 0; i < waypoints.Count; i++)
        {
            if (Within(waypoints[i], world, tolerance))
            {
                return new ConnectorHandleHit(ConnectorHandlePart.Waypoint, i);
            }
        }

        if (centerLabel.Present && HitsLabel(centerLabel, world))
        {
            return new ConnectorHandleHit(ConnectorHandlePart.CenterLabel, -1);
        }

        if (sourceLabel.Present && HitsLabel(sourceLabel, world))
        {
            return new ConnectorHandleHit(ConnectorHandlePart.SourceLabel, -1);
        }

        if (targetLabel.Present && HitsLabel(targetLabel, world))
        {
            return new ConnectorHandleHit(ConnectorHandlePart.TargetLabel, -1);
        }

        return ConnectorHandleHit.None;
    }

    /// <summary>True when <paramref name="point"/> is within <paramref name="tolerance"/> of <paramref name="handle"/> on both axes.</summary>
    public static bool Within(Point2D handle, Point2D point, double tolerance)
        => Math.Abs(handle.X - point.X) <= tolerance && Math.Abs(handle.Y - point.Y) <= tolerance;

    /// <summary>True when <paramref name="world"/> falls inside the estimated text box of <paramref name="label"/>.</summary>
    public static bool HitsLabel(ConnectorLabelBox label, Point2D world)
    {
        double width = Math.Max(MinLabelWidth, label.TextLength * CharWidthEstimate);
        return world.X >= label.TopLeft.X - LabelSlack && world.X <= label.TopLeft.X + width + LabelSlack
            && world.Y >= label.TopLeft.Y - LabelSlack && world.Y <= label.TopLeft.Y + LabelHeight + LabelSlack;
    }
}
