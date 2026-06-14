using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Draw.App.ViewModels;
using Draw.App.Views.Interaction;
using Draw.Diagramming.Geometry;
using Draw.Diagramming.Layout;
using Draw.Model.Connectors;
using Draw.Model.Nodes;
using Draw.Model.Primitives;
using PhosphorIconsAvalonia;

namespace Draw.App.Views;

public partial class DiagramView : UserControl
{
    private const double HandleScreenSize = 10d;

    // Selection accent shared by the resize handles, marquee and connect-preview. These are the single
    // source of truth for the accent colours; DiagramView.axaml references the brushes via x:Static so
    // the hex values aren't duplicated in markup. public so the compiled XAML's x:Static can resolve them.
    public static readonly Color SelectionAccentColor = Color.FromRgb(0x3D, 0x7E, 0xFF);
    public static readonly SolidColorBrush SelectionAccentBrush = new(SelectionAccentColor);

    // Same accent at reduced alpha: the selected-connector halo (0x66) and the marquee fill (40).
    public static readonly SolidColorBrush SelectionHaloBrush = new(Color.FromArgb(0x66, 0x3D, 0x7E, 0xFF));
    public static readonly SolidColorBrush MarqueeFillBrush = new(Color.FromArgb(40, 0x3D, 0x7E, 0xFF));

    // Reference accent (amber) — distinguishes the fixed alignment reference from the blue selection.
    public static readonly Color ReferenceAccentColor = Color.FromRgb(0xF2, 0xA9, 0x3B);
    public static readonly SolidColorBrush ReferenceAccentBrush = new(ReferenceAccentColor);

    // A right-button press that travels less than this (screen px, squared) before release is treated as
    // a click that opens the arrange context menu rather than a pan drag.
    private const double ContextClickThresholdSquared = 16d;

    // A left press on a shape must travel at least this far (screen px, squared) before it starts moving
    // the shape. Below it the press only selects, so clicking to select no longer nudges the shape.
    // Screen-space, so the feel is consistent at any zoom.
    private const double MoveDragThresholdSquared = 16d; // 4px

    private DiagramDocumentViewModel? _vm;

    // All scalar + transient state for the in-progress pointer gesture (see CanvasGestureState).
    private readonly CanvasGestureState _gesture = new();
    private ToolboxViewModel? _toolbox;

    // Connector-edit gestures (endpoint/waypoint/label drags), the selection overlay (handles + reference
    // outlines), and scrollbar sync live in these helpers; all are created in the constructor, once the
    // named controls exist.
    private readonly ConnectorEditController _connectorEdit;
    private readonly OverlayController _overlayController;
    private readonly ViewportScrollController _scroll;

    // Inline row editing (double-tap a row → edit; Enter adds next; Tab navigates; Alt+↑/↓ reorders),
    // shared by class members and entity columns through one generic controller per row kind.
    private readonly InlineRowEditController<ClassMemberViewModel> _memberEditor;
    private readonly InlineRowEditController<EntityColumnViewModel> _columnEditor;

    // Arrow-key nudge coalescing: one undo entry per contiguous run of nudges on the same selection.
    private bool _arrowNudgeUndoCaptured;
    private readonly HashSet<object> _arrowNudgeSelection = new();

    public DiagramView()
    {
        InitializeComponent();

        // Construct the overlay controller before the connector-edit controller: the latter is wired with
        // the RepositionOverlay/RebuildOverlay callbacks, which delegate to the overlay controller.
        _overlayController = new OverlayController(Overlay, HandleScreenSize, () => Zoom, SelectionAccentBrush, ReferenceAccentBrush);
        _connectorEdit = new ConnectorEditController(_gesture, EnsureUndoCaptured, RepositionOverlay, RebuildOverlay);
        _scroll = new ViewportScrollController(HScroll, VScroll, FitCorner);

        _memberEditor = new InlineRowEditController<ClassMemberViewModel>(
            member => OwningNode(member) is { } node ? new ClassMemberRowOwner(node) : null,
            (row, selectAll) => FocusEditorFor(row, selectAll),
            () => Focus());
        _columnEditor = new InlineRowEditController<EntityColumnViewModel>(
            column => OwningEntity(column) is { } node ? new EntityColumnRowOwner(node) : null,
            (row, selectAll) => FocusEditorFor(row, selectAll),
            () => Focus());

        Viewport.PointerPressed += OnPointerPressed;
        Viewport.PointerMoved += OnPointerMoved;
        Viewport.PointerReleased += OnPointerReleased;
        Viewport.PointerWheelChanged += OnPointerWheel;
        Viewport.DoubleTapped += OnDoubleTapped;
        HScroll.Scroll += OnHScroll;
        VScroll.Scroll += OnVScroll;
        Viewport.SizeChanged += (_, _) =>
        {
            UpdateGridBounds();
            PushViewportSize();
            UpdateScrollBars();
        };
        KeyDown += OnKeyDown;
        DataContextChanged += OnDataContextChanged;

        // Accept image files dropped from the OS onto the canvas.
        DragDrop.SetAllowDrop(Viewport, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    // The visible viewport size (screen px) the document VM needs to centre pastes.
    private void PushViewportSize()
    {
        if (_vm is not null)
        {
            _vm.ViewportWidth = Viewport.Bounds.Width;
            _vm.ViewportHeight = Viewport.Bounds.Height;
        }
    }

    private double Zoom => _vm?.Zoom ?? 1d;

    // Reflect the armed-tool state as a crosshair cursor so it's clear the next canvas click places a shape.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _toolbox = GetToolbox();
        if (_toolbox is not null)
        {
            _toolbox.PropertyChanged += OnToolboxPropertyChanged;
        }

        UpdateCursor();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_toolbox is not null)
        {
            _toolbox.PropertyChanged -= OnToolboxPropertyChanged;
            _toolbox = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnToolboxPropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateCursor();

    private void UpdateCursor()
        => Viewport.Cursor = _toolbox is { IsSelectTool: false }
            ? new Cursor(StandardCursorType.Cross)
            : Cursor.Default;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.SelectionChanged -= OnVmSelectionChanged;
        }

        _vm = DataContext as DiagramDocumentViewModel;

        if (_vm is not null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            _vm.SelectionChanged += OnVmSelectionChanged;
        }

        UpdateGrid();
        UpdateTransform();
        RebuildOverlay();
        PushViewportSize();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DiagramDocumentViewModel.Zoom)
            or nameof(DiagramDocumentViewModel.PanX)
            or nameof(DiagramDocumentViewModel.PanY))
        {
            UpdateTransform();
        }
    }

    private void OnVmSelectionChanged(object? sender, EventArgs e) => RebuildOverlay();

    private Point ScreenToWorld(Point screen)
        => new((screen.X - (_vm?.PanX ?? 0)) / Zoom, (screen.Y - (_vm?.PanY ?? 0)) / Zoom);

    private void UpdateTransform()
    {
        double zoom = Zoom;
        World.RenderTransform = new MatrixTransform(new Matrix(zoom, 0, 0, zoom, _vm?.PanX ?? 0, _vm?.PanY ?? 0));
        UpdateGridBounds();

        // Pan rides the world transform for free, so only a zoom change needs the overlay handles
        // resized/repositioned; the scrollbars track pan either way.
        if (zoom != _overlayController.LastZoom)
        {
            RepositionOverlay();
        }
        else
        {
            UpdateScrollBars();
        }
    }

    // Keeps the tiled grid rectangle covering the currently visible world region (pan is unbounded),
    // aligned to grid multiples so the lines coincide with snap positions.
    private void UpdateGridBounds()
    {
        if (_vm is null)
        {
            return;
        }

        double zoom = Zoom <= 0 ? 1d : Zoom;
        double viewWidth = Viewport.Bounds.Width;
        double viewHeight = Viewport.Bounds.Height;
        if (viewWidth <= 0 || viewHeight <= 0)
        {
            viewWidth = 4000;
            viewHeight = 4000;
        }

        double cell = _vm.GridSize > 0 ? _vm.GridSize : 50d;
        double worldLeft = -_vm.PanX / zoom;
        double worldTop = -_vm.PanY / zoom;
        double worldRight = worldLeft + (viewWidth / zoom);
        double worldBottom = worldTop + (viewHeight / zoom);
        double margin = cell * 10d;

        double left = System.Math.Floor((worldLeft - margin) / cell) * cell;
        double top = System.Math.Floor((worldTop - margin) / cell) * cell;
        double right = System.Math.Ceiling((worldRight + margin) / cell) * cell;
        double bottom = System.Math.Ceiling((worldBottom + margin) / cell) * cell;

        Canvas.SetLeft(GridBackground, left);
        Canvas.SetTop(GridBackground, top);
        GridBackground.Width = right - left;
        GridBackground.Height = bottom - top;
    }

    // Delegates to the scroll controller, which maps the union of the content bounds and the visible
    // region onto the two scrollbars (the geometry lives in ViewportScrollMath). Driven from
    // RebuildOverlay/RepositionOverlay and UpdateTransform (zoom/pan/selection/content moves) and the
    // Viewport SizeChanged handler (resize). Pan stays unbounded — the bars only reflect it.
    private void UpdateScrollBars()
    {
        if (_vm is not null)
        {
            _scroll.Sync(_vm, Viewport.Bounds.Width, Viewport.Bounds.Height);
        }
    }

    private void OnHScroll(object? sender, ScrollEventArgs e)
    {
        if (_vm is not null)
        {
            _scroll.OnHorizontalScroll(_vm, e.NewValue);
        }
    }

    private void OnVScroll(object? sender, ScrollEventArgs e)
    {
        if (_vm is not null)
        {
            _scroll.OnVerticalScroll(_vm, e.NewValue);
        }
    }

    private void UpdateGrid()
    {
        double cell = _vm?.GridSize ?? 10d;
        if (cell <= 0)
        {
            GridBackground.Fill = null;
            return;
        }

        Pen pen = new(new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)), 1);
        GeometryDrawing drawing = new()
        {
            Geometry = new RectangleGeometry(new Rect(0, 0, cell, cell)),
            Pen = pen,
        };

        GridBackground.Fill = new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile,
            SourceRect = new RelativeRect(0, 0, cell, cell, RelativeUnit.Absolute),
            DestinationRect = new RelativeRect(0, 0, cell, cell, RelativeUnit.Absolute),
            Stretch = Stretch.None,
        };
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm is null || e.Source is TextBox)
        {
            return;
        }

        EndEditing();
        Focus();

        // A mouse gesture ends the current arrow-nudge run, so the next nudge starts a fresh undo entry.
        _arrowNudgeUndoCaptured = false;

        PointerPoint point = e.GetCurrentPoint(Viewport);
        Point screen = point.Position;
        Point world = ScreenToWorld(screen);

        // Right-clicking a class member or entity column opens its context menu instead of starting a pan.
        if (point.Properties.IsRightButtonPressed && (e.Source as Control)?.DataContext is ClassMemberViewModel or EntityColumnViewModel)
        {
            return;
        }

        if (point.Properties.IsMiddleButtonPressed || point.Properties.IsRightButtonPressed)
        {
            BeginPan(e.Pointer, screen);
            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        int handle = HitTestHandle(world);
        if (handle >= 0 && _vm.SelectedNodes.Count() == 1)
        {
            BeginResize(e.Pointer, handle, _vm.SelectedNodes.First());
            return;
        }

        ToolboxViewModel? toolbox = GetToolbox();

        // Connector mode: drag from a source node to a target node.
        if (toolbox is { IsConnectorMode: true })
        {
            BeginConnectorDrag(e.Pointer, world);
            return;
        }

        if (TryPlaceTool(_vm, toolbox, world))
        {
            return;
        }

        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        // Editing the selected connector's handles takes precedence over node hit-testing, because
        // endpoint handles sit on the shape boundaries and would otherwise be stolen by the node. Shift
        // is reserved for extending the selection, so it never starts a handle edit.
        if (!shift && _vm.SelectedConnector is { } selected && TryBeginConnectorEdit(e.Pointer, selected, world, alt))
        {
            return;
        }

        // Connectors win over nodes within tolerance: they always render in the layer on top of nodes,
        // so a click on a line should select the line even where it crosses a shape's filled body or a
        // system boundary — otherwise the rectangular node hit-test below would always swallow it.
        ConnectorViewModel? connector = _vm.HitTestConnector(new Point2D(world.X, world.Y), 6d / Zoom);
        if (connector is not null)
        {
            BeginConnectorPick(e.Pointer, _vm, connector, world, ctrl, shift);
            return;
        }

        NodeViewModelBase? node = HitTestNode(world);
        if (node is not null)
        {
            BeginNodeMove(_vm, node, screen, world, ctrl, shift);
        }
        else
        {
            BeginMarquee(_vm, world, screen, ctrl, shift);
        }

        e.Pointer.Capture(Viewport);
    }

    // Middle/right-button drag: start panning the canvas.
    private void BeginPan(IPointer pointer, Point screen)
    {
        _gesture.Mode = DragMode.Pan;
        _gesture.LastScreen = screen;
        _gesture.PanStartScreen = screen;
        pointer.Capture(Viewport);
    }

    // Drag on a resize handle of the single selected node.
    private void BeginResize(IPointer pointer, int handle, NodeViewModelBase target)
    {
        _gesture.Mode = DragMode.Resize;
        _gesture.ResizeHandle = handle;
        _gesture.ResizeTarget = target;
        _gesture.UndoCaptured = false;
        pointer.Capture(Viewport);
    }

    // Connector mode: begin dragging a new connector from the source node under the cursor (if any).
    private void BeginConnectorDrag(IPointer pointer, Point world)
    {
        NodeViewModelBase? from = HitTestNode(world);
        if (from is not null)
        {
            _gesture.ConnectSource = from;
            _gesture.Mode = DragMode.Connect;
            StartConnectPreview(from, world);
            pointer.Capture(Viewport);
        }
    }

    // Places the toolbox's active shape/class/use-case/entity tool at the click and reactivates the
    // select tool. Returns true if a tool was placed (the caller then ends the gesture).
    private bool TryPlaceTool(DiagramDocumentViewModel vm, ToolboxViewModel? toolbox, Point world)
    {
        // Shape placement.
        if (toolbox?.SelectedShape is { } tool)
        {
            vm.AddShape(tool.Kind, new Point2D(world.X, world.Y));
            toolbox.ActivateSelectTool();
            return true;
        }

        // Class-node placement.
        if (toolbox?.SelectedClassNode is { } classTool)
        {
            vm.AddClassNode(classTool.Kind, new Point2D(world.X, world.Y));
            toolbox.ActivateSelectTool();
            return true;
        }

        // Use-case-node placement.
        if (toolbox?.SelectedUseCaseNode is { } useCaseTool)
        {
            vm.AddUseCaseNode(useCaseTool.Kind, new Point2D(world.X, world.Y));
            toolbox.ActivateSelectTool();
            return true;
        }

        // UML structural-node placement.
        if (toolbox?.SelectedUmlNode is { } umlTool)
        {
            vm.AddUmlNode(umlTool.Kind, new Point2D(world.X, world.Y));
            toolbox.ActivateSelectTool();
            return true;
        }

        // ER table placement.
        if (toolbox is { SelectedEntity: not null })
        {
            vm.AddEntityNode(new Point2D(world.X, world.Y));
            toolbox.ActivateSelectTool();
            return true;
        }

        return false;
    }

    // If the click lands on a handle of the already-selected connector, begin editing it. Returns true
    // if a connector-edit gesture was started.
    private bool TryBeginConnectorEdit(IPointer pointer, ConnectorViewModel selected, Point world, bool alt)
    {
        if (_vm is null || !_connectorEdit.TryBegin(_vm, selected, world, alt, HandleScreenSize / Zoom))
        {
            return false;
        }

        pointer.Capture(Viewport);
        return true;
    }

    // Click on a connector body: Ctrl+click on a waypoint-capable connector splits it and drags the new
    // bend point (one undo step); Shift+click toggles it in/out of a multi-selection; a plain click
    // selects only this connector.
    private void BeginConnectorPick(IPointer pointer, DiagramDocumentViewModel vm, ConnectorViewModel connector, Point world, bool ctrl, bool shift)
    {
        // Ctrl+click on a straight/orthogonal connector splits it: insert a bend point at the
        // click and immediately drag it (one undo step covers the add + reposition).
        if (ctrl && connector.SupportsWaypoints)
        {
            _connectorEdit.BeginSplit(vm, connector, new Point2D(world.X, world.Y));
            pointer.Capture(Viewport);
            return;
        }

        // Shift extends the selection; a plain click on an unselected connector selects only it, while a
        // plain click on an already-selected one keeps the current (possibly multi-) selection intact.
        if (shift)
        {
            vm.ToggleSelectConnector(connector);
        }
        else if (!connector.IsSelected)
        {
            vm.SelectConnector(connector);
        }

        pointer.Capture(Viewport);
    }

    // Click on a node: update selection (Shift extends across shapes and connectors, Ctrl toggles within
    // shapes only, a plain click selects just this node) and arm a move gesture.
    private void BeginNodeMove(DiagramDocumentViewModel vm, NodeViewModelBase node, Point screen, Point world, bool ctrl, bool shift)
    {
        if (shift)
        {
            vm.ToggleSelectUnified(node);
        }
        else if (ctrl)
        {
            vm.ToggleSelect(node);
        }
        else if (!node.IsSelected)
        {
            vm.SelectOnly(node);
        }

        _gesture.Mode = DragMode.Move;
        _gesture.MoveLastWorld = world;
        _gesture.MoveStartScreen = screen;
        _gesture.MoveThresholdPassed = false;
        _gesture.UndoCaptured = false;
    }

    // Click on empty canvas: begin a marquee selection (additive on Ctrl or Shift).
    private void BeginMarquee(DiagramDocumentViewModel vm, Point world, Point screen, bool ctrl, bool shift)
    {
        bool additive = ctrl || shift;
        _gesture.Mode = DragMode.Marquee;
        _gesture.MarqueeStartWorld = world;
        _gesture.MarqueeStartScreen = screen;
        _gesture.MarqueeAdditive = additive;
        if (!additive)
        {
            vm.ClearSelection();
        }

        StartMarquee(world);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_vm is null || _gesture.Mode == DragMode.None)
        {
            return;
        }

        Point screen = e.GetPosition(Viewport);
        Point world = ScreenToWorld(screen);

        switch (_gesture.Mode)
        {
            case DragMode.Pan:
                HandlePan(_vm, screen);
                break;

            case DragMode.Move:
                HandleNodeMove(_vm, screen, world);
                break;

            case DragMode.Resize when _gesture.ResizeTarget is not null:
                HandleResize(_vm, _gesture.ResizeTarget, world);
                break;

            case DragMode.Marquee:
                UpdateMarquee(world);
                break;

            case DragMode.Connect:
                UpdateConnectPreview(world);
                break;

            case DragMode.EndpointMove:
                _connectorEdit.HandleEndpointDrag(world);
                break;

            case DragMode.WaypointMove:
                _connectorEdit.HandleWaypointDrag(world);
                break;

            case DragMode.LabelMove:
                _connectorEdit.HandleLabelDrag(world);
                break;
        }
    }

    private void HandlePan(DiagramDocumentViewModel vm, Point screen)
    {
        vm.PanX += screen.X - _gesture.LastScreen.X;
        vm.PanY += screen.Y - _gesture.LastScreen.Y;
        _gesture.LastScreen = screen;
    }

    private void HandleNodeMove(DiagramDocumentViewModel vm, Point screen, Point world)
    {
        if (!_gesture.MoveThresholdPassed)
        {
            double mdx = screen.X - _gesture.MoveStartScreen.X;
            double mdy = screen.Y - _gesture.MoveStartScreen.Y;
            if (((mdx * mdx) + (mdy * mdy)) <= MoveDragThresholdSquared)
            {
                return; // still inside the dead-zone: a select-click, not a drag
            }

            _gesture.MoveThresholdPassed = true;
            _gesture.MoveLastWorld = world; // anchor here so the first applied delta is 0 (no jump)
        }

        EnsureUndoCaptured();
        vm.MoveSelectedBy(world.X - _gesture.MoveLastWorld.X, world.Y - _gesture.MoveLastWorld.Y);
        _gesture.MoveLastWorld = world;
        RepositionOverlay();
    }

    private void HandleResize(DiagramDocumentViewModel vm, NodeViewModelBase target, Point world)
    {
        EnsureUndoCaptured();
        vm.SetNodeBounds(target, ResizeBounds(target, _gesture.ResizeHandle, world));
        RepositionOverlay();
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_vm is not null)
        {
            switch (_gesture.Mode)
            {
                case DragMode.Move:
                    FinalizeMove(_vm);
                    break;

                case DragMode.Resize when _gesture.ResizeTarget is not null && _vm.SnapEnabled:
                    FinalizeResize(_vm, _gesture.ResizeTarget);
                    break;

                case DragMode.Marquee:
                    FinalizeMarquee(_vm, e);
                    break;

                case DragMode.Connect:
                    FinalizeConnect(_vm, e);
                    break;

                case DragMode.EndpointMove:
                    _vm.MarkModified();
                    break;

                case DragMode.WaypointMove:
                    _connectorEdit.FinalizeWaypointMove(_vm);
                    break;

                case DragMode.LabelMove:
                    _connectorEdit.FinalizeLabelMove(_vm);
                    break;

                case DragMode.Pan:
                    MaybeShowArrangeMenu(e);
                    break;
            }
        }

        ResetGestureState(e.Pointer);
    }

    // Snap the moved selection to the grid once the drag actually moved (threshold passed).
    private void FinalizeMove(DiagramDocumentViewModel vm)
    {
        if (_gesture.MoveThresholdPassed)
        {
            vm.SnapSelectionToGrid();
        }
    }

    // Snap the resized node's edges to the grid. Only invoked when snapping is enabled (switch guard).
    private void FinalizeResize(DiagramDocumentViewModel vm, NodeViewModelBase target)
    {
        Rect2D snapped = target.Model.Bounds.EdgesSnappedToGrid(vm.GridSize);
        vm.SetNodeBounds(target, snapped);
    }

    // Commit the marquee selection; a bare (non-drag, non-additive) click also dismisses the reference.
    private void FinalizeMarquee(DiagramDocumentViewModel vm, PointerReleasedEventArgs e)
    {
        Point marqueeReleaseScreen = e.GetPosition(Viewport);
        Point world = ScreenToWorld(marqueeReleaseScreen);
        Rect2D rect = Rect2D.FromPoints(
            new Point2D(_gesture.MarqueeStartWorld.X, _gesture.MarqueeStartWorld.Y),
            new Point2D(world.X, world.Y));
        vm.SelectInRect(rect, _gesture.MarqueeAdditive);
        EndMarquee();
        // A bare click on empty canvas (no drag, not additive) dismisses the alignment
        // reference; a marquee drag to select the movers does not.
        double clickDx = marqueeReleaseScreen.X - _gesture.MarqueeStartScreen.X;
        double clickDy = marqueeReleaseScreen.Y - _gesture.MarqueeStartScreen.Y;
        if (!_gesture.MarqueeAdditive && vm.HasReference
            && ((clickDx * clickDx) + (clickDy * clickDy)) <= ContextClickThresholdSquared)
        {
            vm.ClearReference();
        }
    }

    // Drop the in-progress connector onto a target node (when valid) and tear down the preview.
    private void FinalizeConnect(DiagramDocumentViewModel vm, PointerReleasedEventArgs e)
    {
        NodeViewModelBase? target = HitTestNode(ScreenToWorld(e.GetPosition(Viewport)));
        if (_gesture.ConnectSource is not null && target is not null && !ReferenceEquals(target, _gesture.ConnectSource))
        {
            ToolboxViewModel? toolbox = GetToolbox();
            RelationshipKind kind = toolbox?.SelectedConnector?.Kind ?? RelationshipKind.Association;
            vm.AddConnector(_gesture.ConnectSource.Id, target.Id, kind);
            toolbox?.ActivateSelectTool();
        }

        EndConnectPreview();
    }

    // Universal end-of-gesture cleanup: reset the state machine, release the pointer, refresh handles.
    private void ResetGestureState(IPointer pointer)
    {
        _gesture.Reset();
        _connectorEdit.Reset();
        pointer.Capture(null);
        RebuildOverlay();
    }

    // Right-clicking the canvas (a click, not a pan drag) opens the arrange menu. Right-clicking an
    // unselected shape selects it first (mirrors left-click), so the menu works with no prior selection;
    // a shape already in a multi-selection keeps the whole group. Right-clicking empty space leaves the
    // selection untouched, so the menu still opens for an existing selection and stays closed otherwise.
    // Align/Distribute need >=2 / >=3 and grey themselves out below that; Space connections works on a
    // single shape. Right-dragging still pans; the middle button never opens the menu.
    private void MaybeShowArrangeMenu(PointerReleasedEventArgs e)
    {
        if (_vm is null || e.InitialPressMouseButton != MouseButton.Right)
        {
            return;
        }

        Point release = e.GetPosition(Viewport);
        double dx = release.X - _gesture.PanStartScreen.X;
        double dy = release.Y - _gesture.PanStartScreen.Y;
        if (((dx * dx) + (dy * dy)) > ContextClickThresholdSquared)
        {
            return;
        }

        NodeViewModelBase? node = HitTestNode(ScreenToWorld(release));
        if (node is not null && !node.IsSelected)
        {
            _vm.SelectOnly(node);
        }

        if (!_vm.SelectedNodes.Any())
        {
            return;
        }

        ContextMenu menu = BuildArrangeMenu(_vm);
        // Defer so the pan gesture's pointer capture is fully released before the popup opens.
        Dispatcher.UIThread.Post(() => menu.Open(Viewport));
    }

    // Built in code (rather than XAML) so the items bind directly to the document VM's commands — that
    // gives correct enable/disable (Distribute needs >=3 selected) with no DataContext plumbing.
    private static ContextMenu BuildArrangeMenu(DiagramDocumentViewModel vm)
    {
        // Icons mirror the ribbon's Arrange group exactly (MainWindow.axaml): align glyphs come from
        // Phosphor, the rest from the ToolIcon.* geometries in Resources/ToolIcons.axaml.
        MenuItem align = new() { Header = "Align", IsEnabled = vm.CanAlignSelection };
        align.Items.Add(ArrangeItem("Align left", vm.AlignCommand, AlignmentMode.Left, Phosphor(Icon.align_left)));
        align.Items.Add(ArrangeItem("Align center", vm.AlignCommand, AlignmentMode.CenterHorizontal, Phosphor(Icon.align_center_horizontal)));
        align.Items.Add(ArrangeItem("Align right", vm.AlignCommand, AlignmentMode.Right, Phosphor(Icon.align_right)));
        align.Items.Add(new Separator());
        align.Items.Add(ArrangeItem("Align top", vm.AlignCommand, AlignmentMode.Top, Phosphor(Icon.align_top)));
        align.Items.Add(ArrangeItem("Align middle", vm.AlignCommand, AlignmentMode.CenterVertical, Phosphor(Icon.align_center_vertical)));
        align.Items.Add(ArrangeItem("Align bottom", vm.AlignCommand, AlignmentMode.Bottom, Phosphor(Icon.align_bottom)));

        MenuItem order = new() { Header = "Order" };
        order.Items.Add(ArrangeItem("Bring to front", vm.OrderCommand, ZOrderOperation.BringToFront, ToolGeometry("ToolIcon.BringToFront")));
        order.Items.Add(ArrangeItem("Bring forward", vm.OrderCommand, ZOrderOperation.BringForward, ToolGeometry("ToolIcon.BringForward")));
        order.Items.Add(ArrangeItem("Send backward", vm.OrderCommand, ZOrderOperation.SendBackward, ToolGeometry("ToolIcon.SendBackward")));
        order.Items.Add(ArrangeItem("Send to back", vm.OrderCommand, ZOrderOperation.SendToBack, ToolGeometry("ToolIcon.SendToBack")));

        ContextMenu menu = new();
        // Dim icons of disabled items so they read as disabled: Fluent grays the item's text but
        // leaves the icon presenter's PathIcon at full colour. Opacity (not Foreground) because the
        // icon's Foreground is theme-driven, whereas Opacity is unopposed by any local value.
        Style disabledIcon = new(selector => selector.OfType<PathIcon>().Class(":disabled"));
        disabledIcon.Setters.Add(new Setter(Visual.OpacityProperty, 0.4));
        menu.Styles.Add(disabledIcon);
        menu.Items.Add(align);
        menu.Items.Add(order);
        menu.Items.Add(new Separator());
        menu.Items.Add(ArrangeItem("Distribute horizontally", vm.DistributeCommand, DistributionMode.Horizontal, ToolGeometry("ToolIcon.DistributeHorizontal")));
        menu.Items.Add(ArrangeItem("Distribute vertically", vm.DistributeCommand, DistributionMode.Vertical, ToolGeometry("ToolIcon.DistributeVertical")));
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "Space connections", Command = vm.SpaceConnectionsCommand, Icon = MenuIcon(ToolGeometry("ToolIcon.SpaceConnections")) });
        menu.Items.Add(new MenuItem { Header = "Merge connections", Command = vm.MergeConnectionsCommand, Icon = MenuIcon(ToolGeometry("ToolIcon.MergeConnections")) });

        // Relative alignment: capture the selection as a fixed reference, then line a later selection up
        // against it. Align-to-reference items gate on a reference being set + movers selected (CanExecute).
        MenuItem alignToRef = new() { Header = "Align to reference", IsEnabled = vm.CanAlignToReference };
        alignToRef.Items.Add(ArrangeItem("Align left", vm.AlignToReferenceCommand, AlignmentMode.Left, Phosphor(Icon.align_left)));
        alignToRef.Items.Add(ArrangeItem("Align center", vm.AlignToReferenceCommand, AlignmentMode.CenterHorizontal, Phosphor(Icon.align_center_horizontal)));
        alignToRef.Items.Add(ArrangeItem("Align right", vm.AlignToReferenceCommand, AlignmentMode.Right, Phosphor(Icon.align_right)));
        alignToRef.Items.Add(new Separator());
        alignToRef.Items.Add(ArrangeItem("Align top", vm.AlignToReferenceCommand, AlignmentMode.Top, Phosphor(Icon.align_top)));
        alignToRef.Items.Add(ArrangeItem("Align middle", vm.AlignToReferenceCommand, AlignmentMode.CenterVertical, Phosphor(Icon.align_center_vertical)));
        alignToRef.Items.Add(ArrangeItem("Align bottom", vm.AlignToReferenceCommand, AlignmentMode.Bottom, Phosphor(Icon.align_bottom)));

        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "Set as reference", Command = vm.SetReferenceCommand, Icon = MenuIcon(Phosphor(Icon.push_pin)) });
        menu.Items.Add(alignToRef);
        menu.Items.Add(new MenuItem { Header = "Clear reference", Command = vm.ClearReferenceCommand, Icon = MenuIcon(Phosphor(Icon.x)) });
        return menu;
    }

    private static MenuItem ArrangeItem<T>(string header, RelayCommand<T> command, T parameter, Geometry? icon)
        => new() { Header = header, Command = command, CommandParameter = parameter, Icon = MenuIcon(icon) };

    private static PathIcon? MenuIcon(Geometry? geometry)
        => geometry is null ? null : new PathIcon { Data = geometry, Width = 16, Height = 16 };

    private static Geometry Phosphor(Icon icon)
        => IconService.CreateGeometry(icon, IconType.regular);

    private static Geometry? ToolGeometry(string key)
        => Application.Current!.TryGetResource(key, null, out object? value) ? value as Geometry : null;

    /// <summary>Opens a prebuilt tool menu (Shift+S / Shift+C) at the pointer over this canvas.</summary>
    public void OpenToolMenu(ContextMenu menu)
        // Defer so any in-flight pointer capture is released before the popup opens (matches the arrange menu).
        => Dispatcher.UIThread.Post(() => menu.Open(Viewport));

    private void OnPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (_vm is null)
        {
            return;
        }

        Point screen = e.GetPosition(Viewport);

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            Point worldBefore = ScreenToWorld(screen);
            double factor = e.Delta.Y > 0 ? 1.15 : 1d / 1.15;
            double newZoom = Math.Clamp(_vm.Zoom * factor, _vm.MinZoom, _vm.MaxZoom);
            _vm.Zoom = newZoom;
            _vm.PanX = screen.X - (worldBefore.X * newZoom);
            _vm.PanY = screen.Y - (worldBefore.Y * newZoom);
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            _vm.PanX += e.Delta.Y * 40;
        }
        else
        {
            _vm.PanY += e.Delta.Y * 40;
        }

        e.Handled = true;
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_vm is null)
        {
            return;
        }

        Point world = ScreenToWorld(e.GetPosition(Viewport));
        if (HitTestNode(world) is { HasInlineLabel: true } node)
        {
            _vm.CaptureUndo();
            node.IsEditing = true;
            FocusEditorFor(node, selectAll: true);
        }
    }

    /// <summary>Padding (world units) added around the content bounds in every export.</summary>
    private const double ExportPadding = 16d;

    /// <summary>
    /// Renders the diagram content — shapes and connectors only, no grid and no selection overlay — to a
    /// bitmap at a fixed <paramref name="scale"/> (1 = 1 world unit per pixel @ 96 DPI), independent of the
    /// on-screen zoom/pan. The whole diagram is captured (selection is ignored) with a fixed margin; the
    /// background is transparent. Returns <c>null</c> when the diagram is empty.
    /// </summary>
    /// <remarks>
    /// Renders <c>Viewport</c> (not <c>World</c>) so that <c>World</c>'s own <c>RenderTransform</c> is
    /// applied as a child transform — the same proven path the on-screen render uses. The grid, overlay,
    /// viewport clip and viewport background are swapped out and restored synchronously, so the on-screen
    /// view never repaints the intermediate state (this runs after the file picker, with no awaits in
    /// between).
    /// </remarks>
    public RenderTargetBitmap? RenderContentBitmap(double scale = 1d)
    {
        if (_vm?.GetContentBounds() is not { } content)
        {
            return null;
        }

        double width = content.Width + (ExportPadding * 2d);
        double height = content.Height + (ExportPadding * 2d);

        ITransform? savedTransform = World.RenderTransform;
        bool savedClip = Viewport.ClipToBounds;
        IBrush? savedBackground = Viewport.Background;
        bool gridVisible = GridBackground.IsVisible;
        bool overlayVisible = Overlay.IsVisible;
        try
        {
            Viewport.ClipToBounds = false;
            Viewport.Background = null;
            GridBackground.IsVisible = false;
            Overlay.IsVisible = false;
            World.RenderTransform = new MatrixTransform(new Matrix(
                scale, 0, 0, scale,
                (ExportPadding - content.X) * scale,
                (ExportPadding - content.Y) * scale));

            PixelSize pixelSize = new(
                Math.Max(1, (int)Math.Ceiling(width * scale)),
                Math.Max(1, (int)Math.Ceiling(height * scale)));
            RenderTargetBitmap bitmap = new(pixelSize, new Vector(96d, 96d));
            bitmap.Render(Viewport);
            return bitmap;
        }
        finally
        {
            World.RenderTransform = savedTransform;
            Viewport.ClipToBounds = savedClip;
            Viewport.Background = savedBackground;
            GridBackground.IsVisible = gridVisible;
            Overlay.IsVisible = overlayVisible;
            UpdateTransform();
            RebuildOverlay();
        }
    }

    /// <summary>
    /// Builds a full-parity SVG document for the diagram content (shapes + connectors only, no grid or
    /// overlay), zoom-independently at 1:1 with the same fixed margin and transparent background as the
    /// raster export. Returns <c>null</c> when the diagram is empty. Reads the already-laid-out node
    /// controls, so it must be called on the UI thread.
    /// </summary>
    public string? BuildSvgDocument()
    {
        if (_vm?.GetContentBounds() is not { } content)
        {
            return null;
        }

        return Draw.App.Rendering.DiagramSvgRenderer.Render(NodesLayer, _vm.Nodes, _vm.Connectors, content, ExportPadding);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Don't steal keys while editing a node's text; Escape ends editing.
        if (e.Source is TextBox)
        {
            if (e.Key == Key.Escape)
            {
                EndEditing();
            }

            return;
        }

        // Editor commands (copy/cut/paste/duplicate/delete/…) are driven by the window-level keymap
        // dispatcher now. This only ends an in-progress canvas text edit when Escape isn't bound there.
        if (e.Key == Key.Escape)
        {
            EndEditing();
            return;
        }

        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down && _vm is not null)
        {
            NudgeSelection(e.Key, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            e.Handled = true;
        }
    }

    // Arrow keys nudge the current selection: plain = one grid cell, Shift = a 1px fine step. Selected
    // nodes move together; a selected connector with bend points shifts its whole route. Moving by exactly
    // the requested delta (no implicit grid snap, unlike a drag release) keeps the fine step meaningful.
    private void NudgeSelection(Key key, bool fine)
    {
        if (_vm is null)
        {
            return;
        }

        double step = fine ? 1d : _vm.GridSize;
        (double dx, double dy) = key switch
        {
            Key.Left => (-step, 0d),
            Key.Right => (step, 0d),
            Key.Up => (0d, -step),
            Key.Down => (0d, step),
            _ => (0d, 0d),
        };

        if (_vm.SelectedConnector is { } connector && connector.Waypoints.Count > 0)
        {
            EnsureArrowNudgeUndo();
            connector.MoveBendPointsBy(dx, dy);
        }
        else if (_vm.SelectedNodes.Any())
        {
            EnsureArrowNudgeUndo();
            _vm.MoveSelectedBy(dx, dy);
        }
        else
        {
            // Nothing movable selected (no nodes, or an anchored connector with no bend points): no-op.
            return;
        }

        _vm.MarkModified();
        RepositionOverlay();
    }

    // Captures a single undo snapshot at the start of a contiguous nudge run; changing the selection
    // (by mouse, which also clears the flag, or by a command that re-selects) begins a fresh run.
    private void EnsureArrowNudgeUndo()
    {
        if (_vm is null)
        {
            return;
        }

        HashSet<object> current = new(_vm.SelectedNodes);
        if (_vm.SelectedConnector is { } connector)
        {
            current.Add(connector);
        }

        if (!_arrowNudgeUndoCaptured || !current.SetEquals(_arrowNudgeSelection))
        {
            _vm.CaptureUndo();
            _arrowNudgeUndoCaptured = true;
            _arrowNudgeSelection.Clear();
            _arrowNudgeSelection.UnionWith(current);
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        if (_vm is null)
        {
            return;
        }

        IEnumerable<IStorageItem>? files = e.DataTransfer.TryGetFiles();
        if (files is null)
        {
            return;
        }

        Point world = ScreenToWorld(e.GetPosition(Viewport));
        foreach (IStorageItem storageItem in files)
        {
            if (storageItem is not IStorageFile file || file.TryGetLocalPath() is not { } path || !IsImagePath(path))
            {
                continue;
            }

            try
            {
                byte[] data = await File.ReadAllBytesAsync(path);
                _vm.AddImageNode(new Point2D(world.X, world.Y), data, ImageFormatFromPath(path));
                world = new Point(world.X + 16, world.Y + 16); // cascade when several are dropped at once
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Skip an unreadable file; the others still drop.
            }
        }
    }

    private static bool IsImagePath(string path)
        => System.IO.Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif";

    private static string ImageFormatFromPath(string path)
    {
        string ext = System.IO.Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return ext.Length == 0 ? "png" : ext;
    }

    private void OnMemberDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_vm is not null && (sender as Control)?.DataContext is ClassMemberViewModel member)
        {
            _memberEditor.BeginEdit(member);
            e.Handled = true;
        }
    }

    private void OnMemberEditCommitted(object? sender, RoutedEventArgs e)
    {
        if (_vm is not null && (sender as Control)?.DataContext is ClassMemberViewModel member)
        {
            _memberEditor.CommitOnLostFocus(member);
        }
    }

    private void OnMemberEditKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm is not null
            && sender is TextBox { DataContext: ClassMemberViewModel member }
            && _memberEditor.HandleKey(member, e.Key, e.KeyModifiers))
        {
            e.Handled = true;
        }
    }

    private void OnClassBodyLoaded(object? sender, RoutedEventArgs e)
    {
        // Class/interface: fields compartment shrinks to its content, operations fills the slack.
        // Enums keep the XAML default (literals fill), so they are untouched.
        if (sender is Grid grid && grid.DataContext is ClassNodeViewModel node && node.HasOperations)
        {
            grid.RowDefinitions[2].Height = GridLength.Auto;                      // fields
            grid.RowDefinitions[4].Height = new GridLength(1, GridUnitType.Star); // operations
        }
    }

    private void OnCompartmentDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_vm is null || (sender as Control)?.DataContext is not ClassNodeViewModel node)
        {
            return;
        }

        bool operations = (sender as Control)?.Tag as string == "operations";
        MemberKind kind = operations
            ? MemberKind.Operation
            : node.IsEnum ? MemberKind.EnumLiteral : MemberKind.Field;
        AddMemberAndEdit(node, kind, index: -1);
        e.Handled = true;
    }

    private void OnAddFieldClick(object? sender, RoutedEventArgs e)
    {
        if (NodeOf(sender) is { } node)
        {
            AddMemberAndEdit(node, node.IsEnum ? MemberKind.EnumLiteral : MemberKind.Field, index: -1);
        }
    }

    private void OnAddOperationClick(object? sender, RoutedEventArgs e)
    {
        if (NodeOf(sender) is { } node)
        {
            AddMemberAndEdit(node, MemberKind.Operation, index: -1);
        }
    }

    private void OnInsertBelowClick(object? sender, RoutedEventArgs e)
    {
        if (MemberOf(sender) is not { } member || OwningNode(member) is not { } node)
        {
            return;
        }

        (_, int index) = node.Locate(member);
        if (index >= 0)
        {
            AddMemberAndEdit(node, member.Model.Kind, index + 1);
        }
    }

    private void OnMoveMemberUpClick(object? sender, RoutedEventArgs e) => MoveMember(sender, -1);

    private void OnMoveMemberDownClick(object? sender, RoutedEventArgs e) => MoveMember(sender, +1);

    private void OnRemoveMemberClick(object? sender, RoutedEventArgs e)
    {
        if (MemberOf(sender) is { } member && OwningNode(member) is { } node)
        {
            node.RemoveMember(member);
        }
    }

    private void OnSetVisibilityClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: MemberVisibility visibility, DataContext: ClassMemberViewModel member })
        {
            member.Visibility = visibility;
        }
    }

    private void MoveMember(object? sender, int delta)
    {
        if (MemberOf(sender) is { } member && OwningNode(member) is { } node)
        {
            node.MoveMember(member, delta);
        }
    }

    private void AddMemberAndEdit(ClassNodeViewModel node, MemberKind kind, int index)
    {
        ClassMemberViewModel vm = node.InsertNewMember(kind, index);
        TryFocusMemberEditor(vm, selectAll: false);
    }

    private void TryFocusMemberEditor(ClassMemberViewModel member, bool selectAll) => FocusEditorFor(member, selectAll);

    private void TryFocusColumnEditor(EntityColumnViewModel column, bool selectAll) => FocusEditorFor(column, selectAll);

    // Focuses the inline editor whose DataContext is the given row view model (class member or entity column).
    private void FocusEditorFor(object rowDataContext, bool selectAll)
    {
        // Post at Loaded priority so a just-inserted row's container is realized before we focus.
        Dispatcher.UIThread.Post(
            () =>
            {
                TextBox? box = this.GetVisualDescendants()
                    .OfType<TextBox>()
                    .FirstOrDefault(t => ReferenceEquals(t.DataContext, rowDataContext));
                if (box is null)
                {
                    return;
                }

                box.Focus();
                if (selectAll)
                {
                    box.SelectAll();
                }
                else
                {
                    box.CaretIndex = box.Text?.Length ?? 0;
                }
            },
            DispatcherPriority.Loaded);
    }

    private ClassNodeViewModel? OwningNode(ClassMemberViewModel member) =>
        _vm?.Nodes.OfType<ClassNodeViewModel>()
            .FirstOrDefault(n => n.PrimaryMembers.Contains(member) || n.Operations.Contains(member));

    private static ClassNodeViewModel? NodeOf(object? sender) => (sender as Control)?.DataContext as ClassNodeViewModel;

    private static ClassMemberViewModel? MemberOf(object? sender) => (sender as Control)?.DataContext as ClassMemberViewModel;

    // --- Entity (ER table) column inline editing — uses the same InlineRowEditController as members. ---

    private void OnColumnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_vm is not null && (sender as Control)?.DataContext is EntityColumnViewModel column)
        {
            _columnEditor.BeginEdit(column);
            e.Handled = true;
        }
    }

    private void OnColumnEditCommitted(object? sender, RoutedEventArgs e)
    {
        if (_vm is not null && (sender as Control)?.DataContext is EntityColumnViewModel column)
        {
            _columnEditor.CommitOnLostFocus(column);
        }
    }

    private void OnColumnEditKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm is not null
            && sender is TextBox { DataContext: EntityColumnViewModel column }
            && _columnEditor.HandleKey(column, e.Key, e.KeyModifiers))
        {
            e.Handled = true;
        }
    }

    private void OnColumnCompartmentDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_vm is null || (sender as Control)?.DataContext is not EntityNodeViewModel node)
        {
            return;
        }

        AddColumnAndEdit(node, index: -1);
        e.Handled = true;
    }

    private void OnAddColumnClick(object? sender, RoutedEventArgs e)
    {
        if (EntityNodeOf(sender) is { } node)
        {
            AddColumnAndEdit(node, index: -1);
        }
    }

    private void OnInsertColumnBelowClick(object? sender, RoutedEventArgs e)
    {
        if (ColumnOf(sender) is not { } column || OwningEntity(column) is not { } node)
        {
            return;
        }

        (_, int index) = node.Locate(column);
        if (index >= 0)
        {
            AddColumnAndEdit(node, index + 1);
        }
    }

    private void OnMoveColumnUpClick(object? sender, RoutedEventArgs e) => MoveColumn(sender, -1);

    private void OnMoveColumnDownClick(object? sender, RoutedEventArgs e) => MoveColumn(sender, +1);

    private void OnRemoveColumnClick(object? sender, RoutedEventArgs e)
    {
        if (ColumnOf(sender) is { } column && OwningEntity(column) is { } node)
        {
            node.RemoveColumn(column);
        }
    }

    private void MoveColumn(object? sender, int delta)
    {
        if (ColumnOf(sender) is { } column && OwningEntity(column) is { } node)
        {
            node.MoveColumn(column, delta);
        }
    }

    private void AddColumnAndEdit(EntityNodeViewModel node, int index)
    {
        EntityColumnViewModel vm = node.InsertNewColumn(index);
        TryFocusColumnEditor(vm, selectAll: false);
    }

    private EntityNodeViewModel? OwningEntity(EntityColumnViewModel column) =>
        _vm?.Nodes.OfType<EntityNodeViewModel>().FirstOrDefault(n => n.Columns.Contains(column));

    private static EntityNodeViewModel? EntityNodeOf(object? sender) => (sender as Control)?.DataContext as EntityNodeViewModel;

    private static EntityColumnViewModel? ColumnOf(object? sender) => (sender as Control)?.DataContext as EntityColumnViewModel;

    private void EndEditing()
    {
        if (_vm is null)
        {
            return;
        }

        bool labelEdited = false;
        foreach (NodeViewModelBase node in _vm.Nodes.Where(n => n.IsEditing))
        {
            node.IsEditing = false;
            labelEdited = true;
        }

        bool committed = false;
        foreach (ClassNodeViewModel klass in _vm.Nodes.OfType<ClassNodeViewModel>())
        {
            committed |= klass.CommitPendingEdits();
            klass.DiscardEmptyNewMembers();
        }

        foreach (EntityNodeViewModel entity in _vm.Nodes.OfType<EntityNodeViewModel>())
        {
            committed |= entity.CommitPendingEdits();
            entity.DiscardEmptyNewColumns();
        }

        if (labelEdited || committed)
        {
            _vm.MarkModified();
        }
    }

    private NodeViewModelBase? HitTestNode(Point world)
    {
        Point2D p = new(world.X, world.Y);
        // Pick the front-most node under the cursor by stacking order (highest ZIndex), so the result
        // follows user reordering rather than collection order and a shape on a boundary wins over it.
        // On a ZIndex tie, break by collection order and take the last: OrderBy is stable, so equal-ZIndex
        // nodes keep their Nodes order, and Avalonia draws the last of them on top — so LastOrDefault
        // returns exactly the node rendered front-most (MaxBy would return the first/back-most instead).
        return _vm?.Nodes.Where(n => n.Model.Bounds.Contains(p)).OrderBy(n => n.Model.ZIndex).LastOrDefault();
    }

    private int HitTestHandle(Point world)
    {
        if (_vm is null || _vm.SelectedNodes.Count() != 1)
        {
            return -1;
        }

        return NodeHandleGeometry.HitTest(
            _vm.SelectedNodes.First().Model.Bounds, new Point2D(world.X, world.Y), HandleScreenSize / Zoom);
    }

    private static Rect2D ResizeBounds(NodeViewModelBase node, int handle, Point world)
        => NodeHandleGeometry.Resize(
            node.Model.Bounds, handle, new Point2D(world.X, world.Y), node.MinWidth, node.MinHeight, node.LocksAspectRatio);

    private void EnsureUndoCaptured()
    {
        if (!_gesture.UndoCaptured && _vm is not null)
        {
            _vm.CaptureUndo();
            _gesture.UndoCaptured = true;
        }
    }

    // Thin wrappers over the overlay controller, which owns the handle/outline controls. The view keeps
    // scrollbar sync, so both wrappers call UpdateScrollBars after the controller updates the overlay.
    // These two method-group names are also the callbacks wired into the connector-edit controller.
    private void RebuildOverlay()
    {
        _overlayController.Rebuild(_vm);
        UpdateScrollBars();
    }

    private void RepositionOverlay()
    {
        _overlayController.Reposition(_vm);
        UpdateScrollBars();
    }

    private void StartMarquee(Point world)
    {
        _gesture.Marquee = new Rectangle
        {
            Fill = MarqueeFillBrush,
            Stroke = SelectionAccentBrush,
            StrokeThickness = 1d / Zoom,
        };
        Canvas.SetLeft(_gesture.Marquee, world.X);
        Canvas.SetTop(_gesture.Marquee, world.Y);
        Overlay.Children.Add(_gesture.Marquee);
    }

    private void UpdateMarquee(Point world)
    {
        if (_gesture.Marquee is null)
        {
            return;
        }

        double left = Math.Min(world.X, _gesture.MarqueeStartWorld.X);
        double top = Math.Min(world.Y, _gesture.MarqueeStartWorld.Y);
        Canvas.SetLeft(_gesture.Marquee, left);
        Canvas.SetTop(_gesture.Marquee, top);
        _gesture.Marquee.Width = Math.Abs(world.X - _gesture.MarqueeStartWorld.X);
        _gesture.Marquee.Height = Math.Abs(world.Y - _gesture.MarqueeStartWorld.Y);
    }

    private void EndMarquee()
    {
        if (_gesture.Marquee is not null)
        {
            Overlay.Children.Remove(_gesture.Marquee);
            _gesture.Marquee = null;
        }
    }

    private void StartConnectPreview(NodeViewModelBase from, Point world)
    {
        Point2D center = from.Model.Bounds.Center;
        _gesture.ConnectPreview = new Line
        {
            StartPoint = new Point(center.X, center.Y),
            EndPoint = world,
            Stroke = SelectionAccentBrush,
            StrokeThickness = 1.5d / Zoom,
            StrokeDashArray = new AvaloniaList<double> { 4, 2 },
            IsHitTestVisible = false,
        };
        Overlay.Children.Add(_gesture.ConnectPreview);
    }

    private void UpdateConnectPreview(Point world)
    {
        if (_gesture.ConnectPreview is not null)
        {
            _gesture.ConnectPreview.EndPoint = world;
        }
    }

    private void EndConnectPreview()
    {
        if (_gesture.ConnectPreview is not null)
        {
            Overlay.Children.Remove(_gesture.ConnectPreview);
            _gesture.ConnectPreview = null;
        }

        _gesture.ConnectSource = null;
    }

    private ToolboxViewModel? GetToolbox()
        => TopLevel.GetTopLevel(this)?.DataContext as ShellViewModel is { } shell ? shell.Toolbox : null;
}
