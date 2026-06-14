using System;
using System.Collections.Generic;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Draw.App.ViewModels;
using Draw.Diagramming.Geometry;
using Draw.Model.Primitives;

namespace Draw.App.Views.Interaction;

/// <summary>
/// Owns the editor's selection overlay: the node resize handles, the connector endpoint/waypoint
/// handles, and the alignment-reference outlines, all drawn on the zoom-scaled overlay canvas. The view
/// drives it — <see cref="Rebuild"/> when the SET of handles changes, <see cref="Reposition"/> for an
/// in-progress drag or a zoom change — and syncs the scrollbars itself afterwards. The transient marquee
/// and connect-preview stay with the gesture state machine in the view. Mirrors the
/// <c>ConnectorEditController</c>/<c>ViewportScrollController</c> pattern; pure handle geometry lives in
/// <see cref="NodeHandleGeometry"/>.
/// </summary>
internal sealed class OverlayController
{
    private readonly Canvas _overlay;
    private readonly double _handleSize;
    private readonly Func<double> _zoom;
    private readonly IBrush _selectionAccent;
    private readonly IBrush _referenceAccent;

    private readonly List<Rectangle> _handles = new();
    private readonly List<Control> _connectorHandles = new();
    private readonly List<Rectangle> _referenceOutlines = new();

    public OverlayController(Canvas overlay, double handleSize, Func<double> zoom, IBrush selectionAccent, IBrush referenceAccent)
    {
        _overlay = overlay;
        _handleSize = handleSize;
        _zoom = zoom;
        _selectionAccent = selectionAccent;
        _referenceAccent = referenceAccent;
    }

    /// <summary>The zoom the handles were last built/repositioned for. Lets the view skip handle work on
    /// a pan (handles ride the world transform) and resize+reposition only when the zoom changes.</summary>
    public double LastZoom { get; private set; } = 1d;

    private double Zoom => _zoom();

    // Full rebuild: tear down and recreate every selection/connector handle and reference outline. Used
    // when the SET of handles changes (selection, selected connector, reference set, data context) or at
    // the end of a gesture; an in-progress drag uses Reposition instead.
    public void Rebuild(DiagramDocumentViewModel? vm)
    {
        LastZoom = Zoom;

        foreach (Rectangle handle in _handles)
        {
            _overlay.Children.Remove(handle);
        }

        _handles.Clear();

        foreach (Control handle in _connectorHandles)
        {
            _overlay.Children.Remove(handle);
        }

        _connectorHandles.Clear();

        // Reference outlines are rebuilt alongside the handles so they track zoom/selection too
        // (self-contained: clears its own list, and no-ops when there's no VM or reference).
        RebuildReferenceOutlines(vm);

        if (vm is null)
        {
            return;
        }

        // Resize handles trace the bounding box of every selected node — this is what marks the
        // selection (single or multi), so each selected shape is outlined regardless of its kind.
        double size = _handleSize / Zoom;
        foreach (NodeViewModelBase node in vm.SelectedNodes)
        {
            foreach (Point2D position in NodeHandleGeometry.HandlePositions(node.Model.Bounds))
            {
                Rectangle handle = new()
                {
                    Width = size,
                    Height = size,
                    Fill = Brushes.White,
                    Stroke = _selectionAccent,
                    StrokeThickness = 1d / Zoom,
                };
                Canvas.SetLeft(handle, position.X - (size / 2));
                Canvas.SetTop(handle, position.Y - (size / 2));
                _overlay.Children.Add(handle);
                _handles.Add(handle);
            }
        }

        if (vm.SelectedConnector is { } connector)
        {
            DrawConnectorHandles(connector);
        }
    }

    // Lightweight overlay update for an in-progress drag or a zoom change: reposition (and resize) the
    // existing handle controls in place rather than recreating them. The control order here matches how
    // Rebuild created them. If the handle set no longer matches the selection — a set change should have
    // routed through Rebuild — this falls back to a full rebuild, so a missed trigger degrades to the
    // (correct, slower) rebuild rather than mis-tracking.
    public void Reposition(DiagramDocumentViewModel? vm)
    {
        LastZoom = Zoom;

        if (vm is null)
        {
            return;
        }

        double size = _handleSize / Zoom;
        double stroke = 1d / Zoom;

        // Node resize handles: selected nodes × 8 positions, in creation order.
        int index = 0;
        foreach (NodeViewModelBase node in vm.SelectedNodes)
        {
            foreach (Point2D position in NodeHandleGeometry.HandlePositions(node.Model.Bounds))
            {
                if (index >= _handles.Count)
                {
                    Rebuild(vm);
                    return;
                }

                PlaceCenteredHandle(_handles[index], position.X, position.Y, size, stroke);
                index++;
            }
        }

        if (index != _handles.Count)
        {
            Rebuild(vm);
            return;
        }

        // Connector handles: [source endpoint, target endpoint, bend point 0, 1, …].
        if (vm.SelectedConnector is { } connector)
        {
            IReadOnlyList<Point2D> waypoints = connector.Waypoints;
            if (_connectorHandles.Count != waypoints.Count + 2)
            {
                Rebuild(vm);
                return;
            }

            PlaceEndpointHandle(_connectorHandles[0], connector.RouteStart, connector.SourceAnchored, size, stroke);
            PlaceEndpointHandle(_connectorHandles[1], connector.RouteEnd, connector.TargetAnchored, size, stroke);
            for (int i = 0; i < waypoints.Count; i++)
            {
                PlaceCenteredHandle(_connectorHandles[i + 2], waypoints[i].X, waypoints[i].Y, size, stroke);
            }
        }
        else if (_connectorHandles.Count != 0)
        {
            Rebuild(vm);
            return;
        }

        if (!TryRepositionReferenceOutlines(vm, size))
        {
            Rebuild(vm);
        }
    }

    // Centres a square handle (node resize handle or bend point) on a world point and rescales it.
    private static void PlaceCenteredHandle(Control handle, double centerX, double centerY, double size, double stroke)
    {
        handle.Width = size;
        handle.Height = size;
        if (handle is Shape shape)
        {
            shape.StrokeThickness = stroke;
        }

        Canvas.SetLeft(handle, centerX - (size / 2));
        Canvas.SetTop(handle, centerY - (size / 2));
    }

    // Centres an endpoint handle and refreshes its filled/hollow state (anchored vs automatic).
    private void PlaceEndpointHandle(Control handle, Point2D position, bool filled, double size, double stroke)
    {
        PlaceCenteredHandle(handle, position.X, position.Y, size, stroke);
        if (handle is Shape shape)
        {
            shape.Fill = filled ? _selectionAccent : Brushes.White;
        }
    }

    // Marks the fixed alignment-reference shapes with a dashed amber box just outside each one — drawn on
    // the zoom-scaled overlay like the selection handles, but in a distinct colour and shown even when the
    // reference isn't selected. Rebuilt on every Rebuild() pass so it tracks zoom/selection.
    private void RebuildReferenceOutlines(DiagramDocumentViewModel? vm)
    {
        foreach (Rectangle outline in _referenceOutlines)
        {
            _overlay.Children.Remove(outline);
        }

        _referenceOutlines.Clear();

        if (vm is null)
        {
            return;
        }

        double inset = _handleSize / Zoom / 2d;
        double thickness = 1.5d / Zoom;
        foreach (NodeViewModelBase node in vm.ReferenceNodes)
        {
            Rect2D b = node.Model.Bounds;
            Rectangle outline = new()
            {
                Width = b.Width + (inset * 2d),
                Height = b.Height + (inset * 2d),
                Stroke = _referenceAccent,
                StrokeThickness = thickness,
                StrokeDashArray = new AvaloniaList<double> { 4d, 3d },
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(outline, b.Left - inset);
            Canvas.SetTop(outline, b.Top - inset);
            _overlay.Children.Add(outline);
            _referenceOutlines.Add(outline);
        }
    }

    // Repositions/resizes the existing reference outlines in place; returns false (→ caller rebuilds)
    // when the reference set no longer matches the outline controls.
    private bool TryRepositionReferenceOutlines(DiagramDocumentViewModel vm, double size)
    {
        double inset = size / 2d;
        double thickness = 1.5d / Zoom;
        int index = 0;
        foreach (NodeViewModelBase node in vm.ReferenceNodes)
        {
            if (index >= _referenceOutlines.Count)
            {
                return false;
            }

            Rectangle outline = _referenceOutlines[index];
            Rect2D b = node.Model.Bounds;
            outline.Width = b.Width + (inset * 2d);
            outline.Height = b.Height + (inset * 2d);
            outline.StrokeThickness = thickness;
            Canvas.SetLeft(outline, b.Left - inset);
            Canvas.SetTop(outline, b.Top - inset);
            index++;
        }

        return index == _referenceOutlines.Count;
    }

    // Endpoint handles are circles (filled when pinned to a forced anchor, hollow when automatic);
    // bend-point handles are squares — drawn on the zoom-scaled overlay like node handles.
    private void DrawConnectorHandles(ConnectorViewModel connector)
    {
        double size = _handleSize / Zoom;

        AddEndpointHandle(connector.RouteStart, connector.SourceAnchored, size);
        AddEndpointHandle(connector.RouteEnd, connector.TargetAnchored, size);

        foreach (Point2D waypoint in connector.Waypoints)
        {
            Rectangle handle = new()
            {
                Width = size,
                Height = size,
                Fill = Brushes.White,
                Stroke = _selectionAccent,
                StrokeThickness = 1d / Zoom,
            };
            Canvas.SetLeft(handle, waypoint.X - (size / 2));
            Canvas.SetTop(handle, waypoint.Y - (size / 2));
            _overlay.Children.Add(handle);
            _connectorHandles.Add(handle);
        }
    }

    private void AddEndpointHandle(Point2D position, bool filled, double size)
    {
        Ellipse handle = new()
        {
            Width = size,
            Height = size,
            Fill = filled ? _selectionAccent : Brushes.White,
            Stroke = _selectionAccent,
            StrokeThickness = 1d / Zoom,
        };
        Canvas.SetLeft(handle, position.X - (size / 2));
        Canvas.SetTop(handle, position.Y - (size / 2));
        _overlay.Children.Add(handle);
        _connectorHandles.Add(handle);
    }
}
