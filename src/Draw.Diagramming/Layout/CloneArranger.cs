using System;
using System.Collections.Generic;
using System.Linq;
using Draw.Diagramming.Geometry;
using Draw.Model.Connectors;
using Draw.Model.Nodes;
using Draw.Model.Primitives;

namespace Draw.Diagramming.Layout;

/// <summary>
/// Pure duplication of a sub-graph (nodes plus the connectors between them) for copy/paste and
/// duplicate. Produces deep clones with fresh ids, translated (and optionally grid-snapped) bounds and
/// restacked z-indices, leaving the caller to wire the clones into its document / view-model collections.
/// UI-agnostic — no Avalonia, no view-model types.
/// </summary>
public static class CloneArranger
{
    /// <summary>The cloned nodes (in ascending-ZIndex / document-add order) and the cloned connectors.</summary>
    public readonly record struct ClonedGraph(IReadOnlyList<NodeBase> Nodes, IReadOnlyList<Connector> Connectors);

    /// <summary>
    /// Deep-clones <paramref name="sourceNodes"/> and <paramref name="sourceConnectors"/> with fresh ids,
    /// translating each clone's bounds by <paramref name="delta"/> (then snapping its position to
    /// <paramref name="snapGridSize"/> when that is non-null). Clones are restacked relative to
    /// <paramref name="existingNodes"/>: ordinary clones rise above everything (matching new-shape
    /// placement) and <see cref="SystemBoundaryNode"/> clones sink into their reserved band below, each
    /// batch keeping its internal front/back order. Connectors keep their endpoints remapped to the cloned
    /// node ids; a connector whose endpoint is not among the cloned nodes is dropped.
    /// </summary>
    public static ClonedGraph Clone(
        IReadOnlyList<NodeBase> sourceNodes,
        IReadOnlyList<Connector> sourceConnectors,
        IReadOnlyList<NodeBase> existingNodes,
        Point2D delta,
        double? snapGridSize)
    {
        ArgumentNullException.ThrowIfNull(sourceNodes);
        ArgumentNullException.ThrowIfNull(sourceConnectors);
        ArgumentNullException.ThrowIfNull(existingNodes);

        // Reproduce the document's z-banding (App-layer IDocumentEditContext.NextZIndex/NextBackgroundZIndex)
        // exactly: ordinary clones take max+1 and up, boundary clones take min-1 and down. This stays a local
        // computation on purpose — CloneArranger is UI-agnostic (Draw.Diagramming) and takes no dependency on
        // the App seam, and it runs two running counters over a growing batch rather than one call per node.
        // Ordinary placement only ever raises the max and boundary placement only ever lowers the min, so the
        // two counters never interfere — equivalent to recomputing both over the document on every clone.
        int nextOrdinary = existingNodes.Count == 0 ? 0 : existingNodes.Max(n => n.ZIndex) + 1;
        int nextBoundary = (existingNodes.Count == 0 ? 0 : existingNodes.Min(n => n.ZIndex)) - 1;

        Dictionary<Guid, Guid> idMap = new();
        List<NodeBase> clonedNodes = new(sourceNodes.Count);
        // Clone in ascending stacking order so a multi-node duplicate keeps its internal front/back order.
        foreach (NodeBase source in sourceNodes.OrderBy(n => n.ZIndex))
        {
            NodeBase clone = source.Clone();
            Guid newId = Guid.NewGuid();
            idMap[source.Id] = newId;
            clone.Id = newId;
            clone.Bounds = clone.Bounds.Translate(delta.X, delta.Y);
            if (snapGridSize is { } grid)
            {
                clone.Bounds = clone.Bounds.PositionSnappedToGrid(grid);
            }

            clone.ZIndex = clone is SystemBoundaryNode ? nextBoundary-- : nextOrdinary++;
            clonedNodes.Add(clone);
        }

        List<Connector> clonedConnectors = new(sourceConnectors.Count);
        foreach (Connector source in sourceConnectors)
        {
            if (!idMap.TryGetValue(source.SourceNodeId, out Guid newSource)
                || !idMap.TryGetValue(source.TargetNodeId, out Guid newTarget))
            {
                continue;
            }

            Connector clone = source.Clone();
            clone.Id = Guid.NewGuid();
            clone.SourceNodeId = newSource;
            clone.TargetNodeId = newTarget;
            clonedConnectors.Add(clone);
        }

        return new ClonedGraph(clonedNodes, clonedConnectors);
    }
}
