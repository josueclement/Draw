using System.Collections.Generic;
using System.Collections.ObjectModel;
using Draw.Model.Documents;
using Draw.Model.Nodes;
using Draw.Model.Primitives;

namespace Draw.App.ViewModels;

/// <summary>
/// The document-editing seams a <see cref="DiagramDocumentViewModel"/> exposes to the focused
/// coordinators it composes (clipboard, connector spacing, z-order, alignment). The view model
/// stays the façade the view binds to; coordinators depend on this interface, never on the
/// concrete view model.
/// </summary>
/// <remarks>
/// Coordinators must read <see cref="Document"/> on every use — the view model swaps its backing
/// document instance on undo/redo. The <see cref="Nodes"/>/<see cref="Connectors"/> collection
/// instances are stable (only cleared and repopulated, never reassigned), so they may be cached.
/// </remarks>
public interface IDocumentEditContext
{
    /// <summary>The live document. Reassigned on undo/redo — read it every time, never cache.</summary>
    DiagramDocument Document { get; }

    ObservableCollection<NodeViewModelBase> Nodes { get; }

    ObservableCollection<ConnectorViewModel> Connectors { get; }

    IEnumerable<NodeViewModelBase> SelectedNodes { get; }

    double Zoom { get; }

    double ViewportWidth { get; }

    double ViewportHeight { get; }

    double GridSize { get; }

    bool SnapEnabled { get; }

    /// <summary>Builds the node view model matching a model node's concrete type.</summary>
    NodeViewModelBase CreateNodeViewModel(NodeBase node);

    /// <summary>Rebuilds the connector view models from the document after a node-set change.</summary>
    void RebuildConnectors();

    /// <summary>Selects exactly the given nodes (clearing any other selection).</summary>
    void SelectNodes(IReadOnlyCollection<NodeViewModelBase> nodes);

    /// <summary>Deletes the current selection (one undo step).</summary>
    void DeleteSelected();

    /// <summary>Clears the current selection.</summary>
    void ClearSelection();

    /// <summary>Captures a whole-document undo snapshot.</summary>
    void CaptureUndo();

    /// <summary>Marks the document dirty.</summary>
    void MarkModified();

    /// <summary>Re-raises the selection-changed notifications (command CanExecute + bound state).</summary>
    void RaiseSelectionChanged();

    /// <summary>The world-space point at the centre of the visible viewport.</summary>
    Point2D ViewportCenterWorld();
}
