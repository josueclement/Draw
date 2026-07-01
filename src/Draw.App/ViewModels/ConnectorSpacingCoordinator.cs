using System;
using System.Collections.Generic;
using System.Linq;
using Draw.Diagramming.Layout;
using Draw.Model.Primitives;

namespace Draw.App.ViewModels;

/// <summary>
/// Connector-spacing operations for one diagram tab: re-pinning the connector ends that touch the
/// selected shape(s) so they fan out evenly across a side or regroup onto its midpoint. Drives the
/// document through <see cref="IDocumentEditContext"/>; the pure group-by-side / even-spacing plan
/// lives in <see cref="ConnectionDistributor"/>.
/// </summary>
public sealed class ConnectorSpacingCoordinator
{
    private readonly IDocumentEditContext _context;

    public ConnectorSpacingCoordinator(IDocumentEditContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Force-pins every connector end touching the selected shape(s) into evenly spaced positions (one
    /// undo step): on each bounding-box side the ends keep their current order and are re-pinned at equal
    /// gaps, and a side with a single end is centred on that edge.
    /// </summary>
    public void SpaceSelectedConnections()
        => PinConnectionEnds(_context.SelectedNodes.Select(n => n.Id).ToHashSet(), ConnectionDistributor.EvenAnchor, captureUndo: true);

    /// <summary>
    /// Force-pins every connector end touching the selected shape(s) onto the centre of the side it lands
    /// on (one undo step) — the inverse of <see cref="SpaceSelectedConnections"/>. Every end on a given
    /// side collapses to that edge's midpoint (they stack, by design), so the two actions let the user fan
    /// out and regroup a shape's connectors.
    /// </summary>
    public void MergeSelectedConnections()
        => PinConnectionEnds(_context.SelectedNodes.Select(n => n.Id).ToHashSet(), (side, _, _) => ConnectionDistributor.EvenAnchor(side, 0, 1), captureUndo: true);

    /// <summary>
    /// Evenly spaces every connector end touching the given shapes — <see cref="SpaceSelectedConnections"/>
    /// for an explicit shape set rather than the current selection. Pass <paramref name="captureUndo"/> =
    /// false to fold the change into an undo snapshot the caller already took (e.g. duplicate/paste), so
    /// the whole gesture stays a single undo step.
    /// </summary>
    public void SpaceConnectionsForShapes(IReadOnlyCollection<Guid> shapeIds, bool captureUndo)
        => PinConnectionEnds(shapeIds as HashSet<Guid> ?? shapeIds.ToHashSet(), ConnectionDistributor.EvenAnchor, captureUndo);

    /// <summary>
    /// Re-pins every connector end touching the shapes in <paramref name="nodeIds"/>, using
    /// <paramref name="anchorFor"/> to choose the relative (u,v) anchor for the <c>i</c>-th of <c>count</c>
    /// ends on a bounding-box side. Reads the current routes before mutating, so each end is classified by
    /// where it lands now; when <paramref name="captureUndo"/> is true it captures one undo entry on the
    /// first real change, and a no-op (nothing actually changes) never captures.
    /// </summary>
    private void PinConnectionEnds(HashSet<Guid> nodeIds, Func<BoxSide, int, int, Point2D> anchorFor, bool captureUndo)
    {
        if (nodeIds.Count == 0)
        {
            return;
        }

        // Snapshot every end touching one of these shapes (read the current routes up front — a connector's
        // route depends only on its own endpoints, so this is order-independent). Each end also carries the
        // far shape's centre so the planner can order a side by the connected shapes' positions (anti-cross).
        // The pure planner groups by side, orders by that, and returns only the ends whose anchor changes.
        List<ConnectionDistributor.PinningEnd<(ConnectorViewModel Connector, bool IsSource)>> ends = new();
        foreach (ConnectorViewModel connector in _context.Connectors)
        {
            if (nodeIds.Contains(connector.Source.Id))
            {
                ends.Add(new ConnectionDistributor.PinningEnd<(ConnectorViewModel Connector, bool IsSource)>(
                    (connector, true), connector.Source.Id, connector.Source.Bounds, connector.RouteStart, connector.Target.Bounds.Center, connector.SourceAnchor));
            }

            if (nodeIds.Contains(connector.Target.Id))
            {
                ends.Add(new ConnectionDistributor.PinningEnd<(ConnectorViewModel Connector, bool IsSource)>(
                    (connector, false), connector.Target.Id, connector.Target.Bounds, connector.RouteEnd, connector.Source.Bounds.Center, connector.TargetAnchor));
            }
        }

        IReadOnlyList<((ConnectorViewModel Connector, bool IsSource) Token, Point2D Anchor)> plan =
            ConnectionDistributor.PlanPinning(ends, anchorFor);

        // An empty plan means nothing changes — add no undo entry (the no-op contract).
        if (plan.Count == 0)
        {
            return;
        }

        if (captureUndo)
        {
            _context.CaptureUndo();
        }
        foreach (((ConnectorViewModel Connector, bool IsSource) Token, Point2D Anchor) op in plan)
        {
            if (op.Token.IsSource)
            {
                op.Token.Connector.SetSourceAnchor(op.Anchor);
            }
            else
            {
                op.Token.Connector.SetTargetAnchor(op.Anchor);
            }
        }

        _context.MarkModified();
    }
}
