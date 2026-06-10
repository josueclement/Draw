using System;
using System.Collections.Generic;
using System.Linq;
using Draw.Diagramming.Layout;
using Draw.Model.Primitives;

namespace Draw.App.ViewModels;

/// <summary>
/// Alignment, distribution and the transient "align to reference" subsystem for one diagram tab.
/// Drives the document through <see cref="IDocumentEditContext"/>; the pure rectangle math lives in
/// <see cref="ShapeArranger"/>. Owns the per-tab reference-id set, but the view model keeps ownership
/// of the bound commands and their property-changed notifications — it calls
/// <see cref="PruneStaleReferences"/> and re-raises the reference properties from its own
/// selection-changed hub.
/// </summary>
public sealed class AlignmentCoordinator
{
    private readonly IDocumentEditContext _context;

    // Transient (never serialized), per-tab "alignment reference": the node ids that stay put while the
    // current selection (the "movers") is lined up against their combined bounding box. Stale ids are
    // pruned via PruneStaleReferences (driven by the view model's selection-changed hub), so the live
    // reference always reflects existing shapes.
    private readonly HashSet<Guid> _referenceIds = new();

    public AlignmentCoordinator(IDocumentEditContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>Alignment needs at least two shapes to have a common edge/center to line up on.</summary>
    public bool CanAlignSelection => _context.SelectedNodes.Count() >= 2;

    /// <summary>Distribution needs at least three shapes (two anchors plus something to space between).</summary>
    public bool CanDistributeSelection => _context.SelectedNodes.Count() >= 3;

    /// <summary>The captured reference nodes that still exist in the document (stale ids are ignored).</summary>
    public IEnumerable<NodeViewModelBase> ReferenceNodes => _context.Nodes.Where(n => _referenceIds.Contains(n.Id));

    /// <summary>True while an alignment reference is captured (at least one reference node still present).</summary>
    public bool HasReference => ReferenceNodes.Any();

    /// <summary>The selected nodes that will actually move — the selection minus any reference nodes.</summary>
    private IEnumerable<NodeViewModelBase> MoverNodes => _context.SelectedNodes.Where(n => !_referenceIds.Contains(n.Id));

    /// <summary>"Set as reference" needs at least one selected node to capture.</summary>
    public bool CanSetReference => _context.SelectedNodes.Any();

    /// <summary>"Align to reference" needs a captured reference and at least one mover (selected non-reference node).</summary>
    public bool CanAlignToReference => HasReference && MoverNodes.Any();

    /// <summary>Banner text shown while a reference is active.</summary>
    public string ReferenceStatusText
    {
        get
        {
            int count = ReferenceNodes.Count();
            return $"Reference set: {count} {(count == 1 ? "shape" : "shapes")}. Select other shapes, then Align to reference. Esc to clear.";
        }
    }

    /// <summary>
    /// Lines the selected shapes up against the selection's bounding box (one undo step). Positions
    /// are applied exactly — deliberately not re-snapped to the grid, so centers stay pixel-perfect.
    /// </summary>
    public void AlignSelected(AlignmentMode mode) => ArrangeSelected(rects => ShapeArranger.Align(rects, mode), minimum: 2);

    /// <summary>Evens out the gaps between the selected shapes along an axis (one undo step).</summary>
    public void DistributeSelected(DistributionMode mode) => ArrangeSelected(rects => ShapeArranger.Distribute(rects, mode), minimum: 3);

    private void ArrangeSelected(Func<IReadOnlyList<Rect2D>, IReadOnlyList<Rect2D>> arrange, int minimum)
    {
        List<NodeViewModelBase> selected = _context.SelectedNodes.ToList();
        if (selected.Count < minimum)
        {
            return;
        }

        _context.CaptureUndo();
        IReadOnlyList<Rect2D> result = arrange(selected.Select(n => n.Model.Bounds).ToList());
        for (int i = 0; i < selected.Count; i++)
        {
            selected[i].X = result[i].X;
            selected[i].Y = result[i].Y;
        }

        _context.MarkModified();
        // Selection set is unchanged, but its geometry moved — re-raise so the view rebuilds
        // the overlay resize handles at the new positions (they aren't data-bound).
        _context.RaiseSelectionChanged();
    }

    /// <summary>
    /// Captures the current selection as the alignment reference: those shapes stay put while a later
    /// selection is lined up against them. Clears the live selection so the next pick is the movers; the
    /// reference keeps its own highlight independently. Transient (not serialized) and per-tab.
    /// </summary>
    public void SetReference()
    {
        HashSet<Guid> ids = _context.SelectedNodes.Select(n => n.Id).ToHashSet();
        if (ids.Count == 0)
        {
            return;
        }

        _referenceIds.Clear();
        foreach (Guid id in ids)
        {
            _referenceIds.Add(id);
        }

        // Drop the live selection so the user's next pick is the movers; ClearSelection() raises the
        // selection-changed refresh (which also notifies the reference properties + commands).
        _context.ClearSelection();
    }

    /// <summary>Clears the alignment reference. Transient state only — no document mutation, no undo step.</summary>
    public void ClearReference()
    {
        if (_referenceIds.Count == 0)
        {
            return;
        }

        _referenceIds.Clear();
        _context.RaiseSelectionChanged();
    }

    /// <summary>
    /// Lines the movers (the selection minus the reference) up against the reference's combined bounding
    /// box, moving them as one block so their relative layout is preserved (one undo step). The reference
    /// shapes never move; positions are applied exactly — deliberately not re-snapped to the grid.
    /// </summary>
    public void AlignSelectedToReference(AlignmentMode mode)
    {
        List<NodeViewModelBase> movers = MoverNodes.ToList();
        List<NodeViewModelBase> references = ReferenceNodes.ToList();
        if (movers.Count == 0 || references.Count == 0)
        {
            return;
        }

        Rect2D referenceBox = references[0].Model.Bounds;
        for (int i = 1; i < references.Count; i++)
        {
            referenceBox = referenceBox.Union(references[i].Model.Bounds);
        }

        _context.CaptureUndo();
        IReadOnlyList<Rect2D> result = ShapeArranger.AlignToReference(
            movers.Select(n => n.Model.Bounds).ToList(), referenceBox, mode);
        for (int i = 0; i < movers.Count; i++)
        {
            movers[i].X = result[i].X;
            movers[i].Y = result[i].Y;
        }

        _context.MarkModified();
        _context.RaiseSelectionChanged();
    }

    /// <summary>
    /// Drops any reference id whose node no longer exists (deleted, or gone after undo/redo) so the
    /// reference reflects only live shapes — and so a deleted reference can't resurrect on undo. Called by
    /// the view model's selection-changed hub before it re-raises the reference properties.
    /// </summary>
    public void PruneStaleReferences()
    {
        if (_referenceIds.Count == 0)
        {
            return;
        }

        HashSet<Guid> live = _context.Nodes.Select(n => n.Id).ToHashSet();
        _referenceIds.RemoveWhere(id => !live.Contains(id));
    }
}
