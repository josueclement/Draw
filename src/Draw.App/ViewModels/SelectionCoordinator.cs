using System.Collections.Generic;
using Draw.Diagramming.Geometry;
using Draw.Model.Primitives;

namespace Draw.App.ViewModels;

/// <summary>
/// Owns the selection-mutation logic — the loops that flip <c>IsSelected</c> on nodes/connectors and
/// decide what clears what — over the shared <see cref="IDocumentEditContext"/>. The document view model
/// keeps the bound read-model (<c>HasSelection</c>, <c>SelectedConnector</c>, …) and the
/// <c>RaiseSelectionChanged</c> fan-out: its public selection methods delegate here for the mutation,
/// then notify. Extracted from the god view model so the (subtle) selection rules live in one place.
/// </summary>
public sealed class SelectionCoordinator
{
    private readonly IDocumentEditContext _context;

    public SelectionCoordinator(IDocumentEditContext context) => _context = context;

    public void SelectOnly(NodeViewModelBase vm)
    {
        ClearConnectorSelection();
        foreach (NodeViewModelBase n in _context.Nodes)
        {
            n.IsSelected = ReferenceEquals(n, vm);
        }
    }

    public void ToggleSelect(NodeViewModelBase vm)
    {
        ClearConnectorSelection();
        vm.IsSelected = !vm.IsSelected;
    }

    public void SelectNodes(IReadOnlyCollection<NodeViewModelBase> nodes)
    {
        ClearConnectorSelection();
        HashSet<NodeViewModelBase> set = nodes as HashSet<NodeViewModelBase> ?? new HashSet<NodeViewModelBase>(nodes);
        foreach (NodeViewModelBase n in _context.Nodes)
        {
            n.IsSelected = set.Contains(n);
        }
    }

    public void ClearSelection()
    {
        ClearConnectorSelection();
        foreach (NodeViewModelBase n in _context.Nodes)
        {
            n.IsSelected = false;
        }
    }

    public void SelectAll()
    {
        foreach (NodeViewModelBase n in _context.Nodes)
        {
            n.IsSelected = true;
        }

        foreach (ConnectorViewModel c in _context.Connectors)
        {
            c.IsSelected = true;
        }
    }

    public void SelectInRect(Rect2D rect, bool additive)
    {
        foreach (NodeViewModelBase n in _context.Nodes)
        {
            if (!additive)
            {
                n.IsSelected = false;
            }

            if (rect.IntersectsWith(n.Model.Bounds))
            {
                n.IsSelected = true;
            }
        }

        // A connector is grabbed when the marquee overlaps any part of its line (consistent with shapes,
        // which select on overlap). Its flattened route handles curves and bend points.
        foreach (ConnectorViewModel c in _context.Connectors)
        {
            if (!additive)
            {
                c.IsSelected = false;
            }

            if (MarqueeGeometry.IntersectsPolyline(rect, c.GetFlattenedPoints()))
            {
                c.IsSelected = true;
            }
        }
    }

    public void SelectConnector(ConnectorViewModel connector)
    {
        foreach (NodeViewModelBase n in _context.Nodes)
        {
            n.IsSelected = false;
        }

        foreach (ConnectorViewModel c in _context.Connectors)
        {
            c.IsSelected = ReferenceEquals(c, connector);
        }
    }

    public void ToggleSelectConnector(ConnectorViewModel connector) => connector.IsSelected = !connector.IsSelected;

    public void ToggleSelectUnified(NodeViewModelBase node) => node.IsSelected = !node.IsSelected;

    private void ClearConnectorSelection()
    {
        foreach (ConnectorViewModel c in _context.Connectors)
        {
            c.IsSelected = false;
        }
    }
}
