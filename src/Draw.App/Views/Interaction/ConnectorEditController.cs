using System;
using Avalonia;
using Draw.App.ViewModels;
using Draw.Diagramming.Geometry;
using Draw.Model.Primitives;

namespace Draw.App.Views.Interaction;

/// <summary>
/// Drives the edit gestures on the currently selected connector — dragging an endpoint anchor, a bend
/// point or a label, plus the Alt-click "remove/reset" shortcut and the Ctrl-click waypoint split. The
/// pure hit-testing and anchor maths live in <see cref="ConnectorHandleHitTester"/> /
/// <see cref="EndpointAnchorMath"/>; this class owns the gesture sub-state and orchestrates the view
/// model mutations. The owning view keeps the raw pointer events and the overlay-handle drawing, and
/// supplies the two callbacks (ensure-undo, refresh-handles) it already implements.
/// </summary>
internal sealed class ConnectorEditController
{
    private readonly CanvasGestureState _gesture;
    private readonly Action _ensureUndo;
    private readonly Action _repositionHandles;
    private readonly Action _rebuildHandles;

    private bool _editSourceEndpoint;
    private int _editBendIndex = -1;
    private ConnectorLabelKind _editLabelKind;
    private Point _labelGrabDelta;

    public ConnectorEditController(CanvasGestureState gesture, Action ensureUndo, Action repositionHandles, Action rebuildHandles)
    {
        _gesture = gesture;
        _ensureUndo = ensureUndo;
        _repositionHandles = repositionHandles;
        _rebuildHandles = rebuildHandles;
    }

    /// <summary>The connector currently being edited, or <c>null</c> when idle.</summary>
    public ConnectorViewModel? EditConnector { get; private set; }

    /// <summary>
    /// If the click lands on a handle of <paramref name="connector"/>, begin editing it (or, with
    /// <paramref name="alt"/>, apply the discrete remove/reset action) and return <c>true</c>.
    /// <paramref name="tolerance"/> is the handle hit tolerance in world units (screen size / zoom).
    /// </summary>
    public bool TryBegin(DiagramDocumentViewModel vm, ConnectorViewModel connector, Point world, bool alt, double tolerance)
    {
        Point2D w = new(world.X, world.Y);
        ConnectorHandleHit hit = ConnectorHandleHitTester.Hit(
            connector.RouteStart,
            connector.RouteEnd,
            connector.Waypoints,
            LabelBox(connector, ConnectorLabelKind.Center),
            LabelBox(connector, ConnectorLabelKind.Source),
            LabelBox(connector, ConnectorLabelKind.Target),
            w,
            tolerance);

        if (hit.Part == ConnectorHandlePart.None)
        {
            return false;
        }

        if (alt)
        {
            ApplyAltAction(vm, connector, hit);
        }
        else
        {
            BeginDrag(connector, hit, world);
        }

        return true;
    }

    /// <summary>
    /// Ctrl-click split: select <paramref name="connector"/>, insert a bend point at the click and arm a
    /// waypoint drag — all under one undo step (capture is taken here and flagged as already captured).
    /// </summary>
    public void BeginSplit(DiagramDocumentViewModel vm, ConnectorViewModel connector, Point2D world)
    {
        vm.CaptureUndo();
        vm.SelectConnector(connector);
        EditConnector = connector;
        _editBendIndex = connector.InsertBendPointAt(world);
        _gesture.Mode = DragMode.WaypointMove;
        _gesture.UndoCaptured = true;
        _rebuildHandles();
    }

    public void HandleEndpointDrag(Point world)
    {
        if (EditConnector is null)
        {
            return;
        }

        _ensureUndo();
        Rect2D bounds = (_editSourceEndpoint ? EditConnector.Source : EditConnector.Target).Bounds;
        Point2D anchor = EndpointAnchorMath.RelativeAnchor(bounds, new Point2D(world.X, world.Y));
        if (_editSourceEndpoint)
        {
            EditConnector.SetSourceAnchor(anchor);
        }
        else
        {
            EditConnector.SetTargetAnchor(anchor);
        }

        _repositionHandles();
    }

    public void HandleWaypointDrag(Point world)
    {
        if (EditConnector is null)
        {
            return;
        }

        _ensureUndo();
        EditConnector.MoveBendPoint(_editBendIndex, new Point2D(world.X, world.Y));
        _repositionHandles();
    }

    public void HandleLabelDrag(Point world)
    {
        if (EditConnector is null)
        {
            return;
        }

        _ensureUndo();
        Point2D natural = EditConnector.NaturalLabelAnchor(_editLabelKind);
        EditConnector.SetLabelOffset(
            _editLabelKind,
            new Point2D((world.X + _labelGrabDelta.X) - natural.X, (world.Y + _labelGrabDelta.Y) - natural.Y));
        _repositionHandles();
    }

    public void FinalizeWaypointMove(DiagramDocumentViewModel vm)
    {
        if (EditConnector is null)
        {
            return;
        }

        if (vm.SnapEnabled)
        {
            EditConnector.SnapBendPointToGrid(_editBendIndex, vm.GridSize);
        }

        vm.MarkModified();
    }

    public void FinalizeLabelMove(DiagramDocumentViewModel vm)
    {
        if (EditConnector is null)
        {
            return;
        }

        if (vm.SnapEnabled)
        {
            EditConnector.SnapLabelToGrid(_editLabelKind, vm.GridSize);
        }

        vm.MarkModified();
    }

    /// <summary>Clears the connector-edit sub-state at the end of a gesture.</summary>
    public void Reset()
    {
        EditConnector = null;
        _editBendIndex = -1;
    }

    // Alt = remove/reset (a discrete action, not a drag), consistent across all three handle kinds.
    private void ApplyAltAction(DiagramDocumentViewModel vm, ConnectorViewModel connector, ConnectorHandleHit hit)
    {
        vm.CaptureUndo();
        switch (hit.Part)
        {
            case ConnectorHandlePart.SourceEndpoint:
                connector.SetSourceAnchor(null);
                break;
            case ConnectorHandlePart.TargetEndpoint:
                connector.SetTargetAnchor(null);
                break;
            case ConnectorHandlePart.Waypoint:
                connector.RemoveBendPoint(hit.WaypointIndex);
                break;
            case ConnectorHandlePart.CenterLabel:
                connector.SetLabelOffset(ConnectorLabelKind.Center, null);
                break;
            case ConnectorHandlePart.SourceLabel:
                connector.SetLabelOffset(ConnectorLabelKind.Source, null);
                break;
            case ConnectorHandlePart.TargetLabel:
                connector.SetLabelOffset(ConnectorLabelKind.Target, null);
                break;
        }

        vm.MarkModified();
        _gesture.Mode = DragMode.None;
        EditConnector = null;
        _rebuildHandles();
    }

    private void BeginDrag(ConnectorViewModel connector, ConnectorHandleHit hit, Point world)
    {
        EditConnector = connector;
        switch (hit.Part)
        {
            case ConnectorHandlePart.SourceEndpoint:
                _editSourceEndpoint = true;
                _gesture.Mode = DragMode.EndpointMove;
                break;
            case ConnectorHandlePart.TargetEndpoint:
                _editSourceEndpoint = false;
                _gesture.Mode = DragMode.EndpointMove;
                break;
            case ConnectorHandlePart.Waypoint:
                _editBendIndex = hit.WaypointIndex;
                _gesture.Mode = DragMode.WaypointMove;
                break;
            case ConnectorHandlePart.CenterLabel:
                BeginLabelDrag(connector, ConnectorLabelKind.Center, world);
                break;
            case ConnectorHandlePart.SourceLabel:
                BeginLabelDrag(connector, ConnectorLabelKind.Source, world);
                break;
            case ConnectorHandlePart.TargetLabel:
                BeginLabelDrag(connector, ConnectorLabelKind.Target, world);
                break;
        }

        _gesture.UndoCaptured = false;
    }

    private void BeginLabelDrag(ConnectorViewModel connector, ConnectorLabelKind kind, Point world)
    {
        _editLabelKind = kind;
        Point2D display = connector.LabelDisplay(kind);
        _labelGrabDelta = new Point(display.X - world.X, display.Y - world.Y);
        _gesture.Mode = DragMode.LabelMove;
    }

    private static ConnectorLabelBox LabelBox(ConnectorViewModel connector, ConnectorLabelKind kind)
    {
        (bool present, string? text) = kind switch
        {
            ConnectorLabelKind.Center => (connector.HasCenterLabel, connector.CenterLabelText),
            ConnectorLabelKind.Source => (connector.HasSourceLabel, connector.SourceLabelText),
            _ => (connector.HasTargetLabel, connector.TargetLabelText),
        };

        Point2D topLeft = connector.LabelDisplay(kind);
        return new ConnectorLabelBox(present, topLeft, text?.Length ?? 0);
    }
}
