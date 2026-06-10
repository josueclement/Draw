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
    public void SpaceSelectedConnections() => PinSelectedConnectionEnds(ConnectionDistributor.EvenAnchor);

    /// <summary>
    /// Force-pins every connector end touching the selected shape(s) onto the centre of the side it lands
    /// on (one undo step) — the inverse of <see cref="SpaceSelectedConnections"/>. Every end on a given
    /// side collapses to that edge's midpoint (they stack, by design), so the two actions let the user fan
    /// out and regroup a shape's connectors.
    /// </summary>
    public void MergeSelectedConnections()
        => PinSelectedConnectionEnds((side, _, _) => ConnectionDistributor.EvenAnchor(side, 0, 1));

    /// <summary>
    /// Re-pins every connector end touching the selected shape(s), using <paramref name="anchorFor"/> to
    /// choose the relative (u,v) anchor for the <c>i</c>-th of <c>count</c> ends on a bounding-box side.
    /// Reads the current routes before mutating, so each end is classified by where it lands now; captures
    /// one undo entry on the first real change, and a no-op (nothing actually changes) adds no undo entry.
    /// </summary>
    private void PinSelectedConnectionEnds(Func<BoxSide, int, int, Point2D> anchorFor)
    {
        HashSet<Guid> selectedIds = _context.SelectedNodes.Select(n => n.Id).ToHashSet();
        if (selectedIds.Count == 0)
        {
            return;
        }

        // Snapshot every end touching a selected shape (read the current routes up front — a connector's
        // route depends only on its own endpoints, so this is order-independent). The pure planner groups
        // by side, keeps the current order, and returns only the ends whose anchor actually changes.
        List<ConnectionDistributor.PinningEnd<(ConnectorViewModel Connector, bool IsSource)>> ends = new();
        foreach (ConnectorViewModel connector in _context.Connectors)
        {
            if (selectedIds.Contains(connector.Source.Id))
            {
                ends.Add(new ConnectionDistributor.PinningEnd<(ConnectorViewModel Connector, bool IsSource)>(
                    (connector, true), connector.Source.Id, connector.Source.Bounds, connector.RouteStart, connector.SourceAnchor));
            }

            if (selectedIds.Contains(connector.Target.Id))
            {
                ends.Add(new ConnectionDistributor.PinningEnd<(ConnectorViewModel Connector, bool IsSource)>(
                    (connector, false), connector.Target.Id, connector.Target.Bounds, connector.RouteEnd, connector.TargetAnchor));
            }
        }

        IReadOnlyList<((ConnectorViewModel Connector, bool IsSource) Token, Point2D Anchor)> plan =
            ConnectionDistributor.PlanPinning(ends, anchorFor);

        // An empty plan means nothing changes — add no undo entry (the no-op contract).
        if (plan.Count == 0)
        {
            return;
        }

        _context.CaptureUndo();
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
