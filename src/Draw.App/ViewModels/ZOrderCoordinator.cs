using System;
using System.Collections.Generic;
using System.Linq;
using Draw.Diagramming.Layout;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>
/// Z-order (front-to-back stacking) operations for one diagram tab. Drives the document through
/// <see cref="IDocumentEditContext"/>; the banded reorder itself is the pure
/// <see cref="ZOrderArranger.ReorderInBands{T}"/>, leaving this coordinator only the collection
/// restack and bound-property refresh.
/// </summary>
public sealed class ZOrderCoordinator
{
    private readonly IDocumentEditContext _context;

    public ZOrderCoordinator(IDocumentEditContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Changes the front-to-back stacking of the selected shapes (one undo step). Nodes are kept in two
    /// bands — system boundaries always below ordinary shapes — and each band is reordered independently,
    /// so a boundary can be re-stacked relative to other boundaries but can never rise above a shape. The
    /// new order is repacked into contiguous <c>ZIndex</c> values; connectors render in their own layer
    /// above all nodes, so they stay on top regardless. A no-op (no undo) when nothing actually moves.
    /// </summary>
    public void ReorderSelected(ZOrderOperation operation)
    {
        HashSet<NodeViewModelBase> selectedSet = _context.SelectedNodes.ToHashSet();
        if (selectedSet.Count == 0)
        {
            return;
        }

        // Boundaries form a lower band that ordinary shapes can never sink below; the banded reorder
        // (back-to-front by current ZIndex within each band) lives in the testable Diagramming layer.
        IReadOnlyList<NodeViewModelBase> reordered = ZOrderArranger.ReorderInBands(
            _context.Nodes,
            n => n.Model is SystemBoundaryNode,
            selectedSet.Contains,
            n => n.Model.ZIndex,
            operation);

        // The current banded order (boundaries first, each band by ZIndex) equals the reorder when nothing
        // moves — so this detects a no-op (e.g. the selection is already at the front) and avoids dirtying.
        IReadOnlyList<NodeViewModelBase> current = _context.Nodes
            .OrderByDescending(n => n.Model is SystemBoundaryNode)
            .ThenBy(n => n.Model.ZIndex)
            .ToList();
        if (reordered.SequenceEqual(current))
        {
            return;
        }

        _context.CaptureUndo();
        for (int i = 0; i < reordered.Count; i++)
        {
            reordered[i].Model.ZIndex = i;
        }

        // Mirror the new order into the node collection so the ItemsControl restacks: render order
        // follows collection order (and the bound ZIndex agrees with it). In-place Move keeps the
        // existing view-model instances — and thus their selection and decoded image bitmaps — intact.
        for (int target = 0; target < reordered.Count; target++)
        {
            int currentIndex = _context.Nodes.IndexOf(reordered[target]);
            if (currentIndex != target)
            {
                _context.Nodes.Move(currentIndex, target);
            }
        }

        // Refresh the bound ZIndex (Visual.ZIndex) too, so it stays consistent with the new order.
        foreach (NodeViewModelBase node in _context.Nodes)
        {
            node.RaiseZIndexChanged();
        }

        _context.MarkModified();
    }
}
