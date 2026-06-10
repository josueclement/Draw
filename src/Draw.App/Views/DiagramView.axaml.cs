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

    // Selection accent shared by the resize handles, marquee and connect-preview.
    private static readonly Color SelectionAccentColor = Color.Parse("#3D7EFF");
    private static readonly SolidColorBrush SelectionAccentBrush = new(SelectionAccentColor);

    // Reference accent (amber) — distinguishes the fixed alignment reference from the blue selection.
    private static readonly SolidColorBrush ReferenceAccentBrush = new(Color.Parse("#F2A93B"));

    // A right-button press that travels less than this (screen px, squared) before release is treated as
    // a click that opens the arrange context menu rather than a pan drag.
    private const double ContextClickThresholdSquared = 16d;

    // A left press on a shape must travel at least this far (screen px, squared) before it starts moving
    // the shape. Below it the press only selects, so clicking to select no longer nudges the shape.
    // Screen-space, so the feel is consistent at any zoom.
    private const double MoveDragThresholdSquared = 16d; // 4px

    private readonly List<Rectangle> _handles = new();
    private readonly List<Control> _connectorHandles = new();
    private readonly List<Rectangle> _referenceOutlines = new();

    // The zoom the overlay handles were last built/repositioned for. Lets UpdateTransform skip handle
    // work on a pan (they ride the world transform) and resize+reposition only when zoom changes.
    private double _lastHandleZoom = 1d;
    private DiagramDocumentViewModel? _vm;

    // All scalar + transient state for the in-progress pointer gesture (see CanvasGestureState).
    private readonly CanvasGestureState _gesture = new();
    private ToolboxViewModel? _toolbox;

    // Connector-edit gestures (endpoint/waypoint/label drags) and scrollbar sync live in these helpers;
    // both are created in the constructor, once the named controls exist.
    private readonly ConnectorEditController _connectorEdit;
    private readonly ViewportScrollController _scroll;

    // Arrow-key nudge coalescing: one undo entry per contiguous run of nudges on the same selection.
    private bool _arrowNudgeUndoCaptured;
    private readonly HashSet<object> _arrowNudgeSelection = new();

    public DiagramView()
    {
        InitializeComponent();

        _connectorEdit = new ConnectorEditController(_gesture, EnsureUndoCaptured, RepositionOverlay, RebuildOverlay);
        _scroll = new ViewportScrollController(HScroll, VScroll, FitCorner);

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
        if (zoom != _lastHandleZoom)
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

        // Editing the selected connector's handles takes precedence over node hit-testing, because
        // endpoint handles sit on the shape boundaries and would otherwise be stolen by the node.
        if (_vm.SelectedConnector is { } selected && TryBeginConnectorEdit(e.Pointer, selected, world, alt))
        {
            return;
        }

        // Connectors win over nodes within tolerance: they always render in the layer on top of nodes,
        // so a click on a line should select the line even where it crosses a shape's filled body or a
        // system boundary — otherwise the rectangular node hit-test below would always swallow it.
        ConnectorViewModel? connector = _vm.HitTestConnector(new Point2D(world.X, world.Y), 6d / Zoom);
        if (connector is not null)
        {
            BeginConnectorPick(e.Pointer, _vm, connector, world, ctrl);
            return;
        }

        NodeViewModelBase? node = HitTestNode(world);
        if (node is not null)
        {
            BeginNodeMove(_vm, node, screen, world, ctrl);
        }
        else
        {
            BeginMarquee(_vm, world, screen, ctrl);
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
    // bend point (one undo step); otherwise just select the connector.
    private void BeginConnectorPick(IPointer pointer, DiagramDocumentViewModel vm, ConnectorViewModel connector, Point world, bool ctrl)
    {
        // Ctrl+click on a straight/orthogonal connector splits it: insert a bend point at the
        // click and immediately drag it (one undo step covers the add + reposition).
        if (ctrl && connector.SupportsWaypoints)
        {
            _connectorEdit.BeginSplit(vm, connector, new Point2D(world.X, world.Y));
            pointer.Capture(Viewport);
            return;
        }

        vm.SelectConnector(connector);
        pointer.Capture(Viewport);
    }

    // Click on a node: update selection (toggle on Ctrl, select-only otherwise) and arm a move gesture.
    private void BeginNodeMove(DiagramDocumentViewModel vm, NodeViewModelBase node, Point screen, Point world, bool ctrl)
    {
        if (ctrl)
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

    // Click on empty canvas: begin a marquee selection (additive on Ctrl).
    private void BeginMarquee(DiagramDocumentViewModel vm, Point world, Point screen, bool ctrl)
    {
        _gesture.Mode = DragMode.Marquee;
        _gesture.MarqueeStartWorld = world;
        _gesture.MarqueeStartScreen = screen;
        _gesture.MarqueeAdditive = ctrl;
        if (!ctrl)
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
            double newZoom = Math.Clamp(_vm.Zoom * factor, 0.1, 8d);
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
        if (_vm is null || (sender as Control)?.DataContext is not ClassMemberViewModel member)
        {
            return;
        }

        // Undo is captured lazily on commit (only if the text actually changed).
        member.BeginEdit();
        e.Handled = true;
        TryFocusMemberEditor(member, selectAll: true);
    }

    private void OnMemberEditCommitted(object? sender, RoutedEventArgs e)
    {
        if (_vm is null || (sender as Control)?.DataContext is not ClassMemberViewModel member)
        {
            return;
        }

        if (member.IsEditing)
        {
            member.CommitEdit();
        }

        // After focus settles, drop an abandoned blank — unless focus moved to another member
        // editor of the same node (the Enter-adds-next / Tab flow keeps the session alive).
        if (OwningNode(member) is { } node)
        {
            Dispatcher.UIThread.Post(
                () =>
                {
                    if (!IsEditingMemberOf(node))
                    {
                        node.DiscardEmptyNewMembers();
                    }
                },
                DispatcherPriority.Background);
        }
    }

    private void OnMemberEditKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm is null
            || sender is not TextBox { DataContext: ClassMemberViewModel member }
            || OwningNode(member) is not { } node)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                CommitAndAddNext(node, member);
                e.Handled = true;
                break;
            case Key.Tab:
                NavigateMember(node, member, e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : +1);
                e.Handled = true;
                break;
            case Key.Up when e.KeyModifiers.HasFlag(KeyModifiers.Alt):
                node.MoveMember(member, -1);
                TryFocusMemberEditor(member, selectAll: false);
                e.Handled = true;
                break;
            case Key.Down when e.KeyModifiers.HasFlag(KeyModifiers.Alt):
                node.MoveMember(member, +1);
                TryFocusMemberEditor(member, selectAll: false);
                e.Handled = true;
                break;
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

    private void CommitAndAddNext(ClassNodeViewModel node, ClassMemberViewModel member)
    {
        member.CommitEdit();
        if (string.IsNullOrWhiteSpace(member.Name))
        {
            // Enter on an empty row finishes entry rather than spawning another blank.
            node.DiscardEmptyNewMembers();
            Focus();
            return;
        }

        (_, int index) = node.Locate(member);
        AddMemberAndEdit(node, member.Model.Kind, index + 1);
    }

    private void NavigateMember(ClassNodeViewModel node, ClassMemberViewModel member, int delta)
    {
        member.CommitEdit();
        if (string.IsNullOrWhiteSpace(member.Name))
        {
            node.DiscardEmptyNewMembers();
            Focus();
            return;
        }

        (System.Collections.ObjectModel.ObservableCollection<ClassMemberViewModel> list, int index) = node.Locate(member);
        int next = index + delta;
        if (index < 0 || next < 0)
        {
            Focus();
            return;
        }

        if (next >= list.Count)
        {
            // Past the end → behave like Enter and add a fresh member of the same kind.
            AddMemberAndEdit(node, member.Model.Kind, index: -1);
            return;
        }

        ClassMemberViewModel target = list[next];
        target.BeginEdit();
        TryFocusMemberEditor(target, selectAll: true);
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

    private static bool IsEditingMemberOf(ClassNodeViewModel node) =>
        node.PrimaryMembers.Concat(node.Operations).Any(m => m.IsEditing);

    // --- Entity (ER table) column inline editing — mirrors the class-member handlers above. ---

    private void OnColumnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_vm is null || (sender as Control)?.DataContext is not EntityColumnViewModel column)
        {
            return;
        }

        column.BeginEdit();
        e.Handled = true;
        TryFocusColumnEditor(column, selectAll: true);
    }

    private void OnColumnEditCommitted(object? sender, RoutedEventArgs e)
    {
        if (_vm is null || (sender as Control)?.DataContext is not EntityColumnViewModel column)
        {
            return;
        }

        if (column.IsEditing)
        {
            column.CommitEdit();
        }

        if (OwningEntity(column) is { } node)
        {
            Dispatcher.UIThread.Post(
                () =>
                {
                    if (!IsEditingColumnOf(node))
                    {
                        node.DiscardEmptyNewColumns();
                    }
                },
                DispatcherPriority.Background);
        }
    }

    private void OnColumnEditKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm is null
            || sender is not TextBox { DataContext: EntityColumnViewModel column }
            || OwningEntity(column) is not { } node)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                CommitAndAddNextColumn(node, column);
                e.Handled = true;
                break;
            case Key.Tab:
                NavigateColumn(node, column, e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : +1);
                e.Handled = true;
                break;
            case Key.Up when e.KeyModifiers.HasFlag(KeyModifiers.Alt):
                node.MoveColumn(column, -1);
                TryFocusColumnEditor(column, selectAll: false);
                e.Handled = true;
                break;
            case Key.Down when e.KeyModifiers.HasFlag(KeyModifiers.Alt):
                node.MoveColumn(column, +1);
                TryFocusColumnEditor(column, selectAll: false);
                e.Handled = true;
                break;
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

    private void CommitAndAddNextColumn(EntityNodeViewModel node, EntityColumnViewModel column)
    {
        column.CommitEdit();
        if (string.IsNullOrWhiteSpace(column.Name))
        {
            node.DiscardEmptyNewColumns();
            Focus();
            return;
        }

        (_, int index) = node.Locate(column);
        AddColumnAndEdit(node, index + 1);
    }

    private void NavigateColumn(EntityNodeViewModel node, EntityColumnViewModel column, int delta)
    {
        column.CommitEdit();
        if (string.IsNullOrWhiteSpace(column.Name))
        {
            node.DiscardEmptyNewColumns();
            Focus();
            return;
        }

        (System.Collections.ObjectModel.ObservableCollection<EntityColumnViewModel> list, int index) = node.Locate(column);
        int next = index + delta;
        if (index < 0 || next < 0)
        {
            Focus();
            return;
        }

        if (next >= list.Count)
        {
            AddColumnAndEdit(node, index: -1);
            return;
        }

        EntityColumnViewModel target = list[next];
        target.BeginEdit();
        TryFocusColumnEditor(target, selectAll: true);
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

    private static bool IsEditingColumnOf(EntityNodeViewModel node) => node.Columns.Any(c => c.IsEditing);

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

        double tolerance = HandleScreenSize / Zoom;
        Point[] positions = HandlePositions(_vm.SelectedNodes.First());
        for (int i = 0; i < positions.Length; i++)
        {
            if (Math.Abs(world.X - positions[i].X) <= tolerance && Math.Abs(world.Y - positions[i].Y) <= tolerance)
            {
                return i;
            }
        }

        return -1;
    }

    private static Point[] HandlePositions(NodeViewModelBase node)
    {
        Rect2D b = node.Model.Bounds;
        double cx = b.X + (b.Width / 2);
        double cy = b.Y + (b.Height / 2);
        return new[]
        {
            new Point(b.Left, b.Top),    // 0 NW
            new Point(cx, b.Top),        // 1 N
            new Point(b.Right, b.Top),   // 2 NE
            new Point(b.Right, cy),      // 3 E
            new Point(b.Right, b.Bottom),// 4 SE
            new Point(cx, b.Bottom),     // 5 S
            new Point(b.Left, b.Bottom), // 6 SW
            new Point(b.Left, cy),       // 7 W
        };
    }

    private static Rect2D ResizeBounds(NodeViewModelBase node, int handle, Point world)
    {
        Rect2D b = node.Model.Bounds;
        double l = b.Left;
        double t = b.Top;
        double r = b.Right;
        double bottom = b.Bottom;

        switch (handle)
        {
            case 0: l = world.X; t = world.Y; break;
            case 1: t = world.Y; break;
            case 2: r = world.X; t = world.Y; break;
            case 3: r = world.X; break;
            case 4: r = world.X; bottom = world.Y; break;
            case 5: bottom = world.Y; break;
            case 6: l = world.X; bottom = world.Y; break;
            case 7: l = world.X; break;
        }

        double left = Math.Min(l, r);
        double top = Math.Min(t, bottom);
        double width = Math.Max(node.MinWidth, Math.Abs(r - l));
        double height = Math.Max(node.MinHeight, Math.Abs(bottom - t));
        Rect2D result = new(left, top, width, height);

        if (node.LocksAspectRatio && b.Width > 0 && b.Height > 0)
        {
            result = LockAspect(handle, b, result, node.MinWidth, node.MinHeight);
        }

        return result;
    }

    // Re-proportions an unconstrained resize so the node keeps its original aspect ratio, anchored at the
    // fixed corner/edge of the dragged handle. Corner handles scale from the opposite corner; edge handles
    // scale (driven by the moved axis) and re-centre on the fixed edge.
    private static Rect2D LockAspect(int handle, Rect2D original, Rect2D raw, double minWidth, double minHeight)
    {
        double ratio = original.Width / original.Height;

        double scale = handle switch
        {
            1 or 5 => raw.Height / original.Height,                                   // N / S: height-driven
            3 or 7 => raw.Width / original.Width,                                     // E / W: width-driven
            _ => Math.Max(raw.Width / original.Width, raw.Height / original.Height),  // corners: dominant axis
        };

        // Don't let either dimension fall below its minimum.
        scale = Math.Max(scale, Math.Max(minWidth / original.Width, minHeight / original.Height));

        double newWidth = original.Width * scale;
        double newHeight = newWidth / ratio;

        double cx = original.Center.X;
        double cy = original.Center.Y;
        (double newLeft, double newTop) = handle switch
        {
            0 => (original.Right - newWidth, original.Bottom - newHeight),  // NW: fix SE corner
            2 => (original.Left, original.Bottom - newHeight),              // NE: fix SW corner
            4 => (original.Left, original.Top),                             // SE: fix NW corner
            6 => (original.Right - newWidth, original.Top),                 // SW: fix NE corner
            1 => (cx - (newWidth / 2), original.Bottom - newHeight),        // N: fix bottom edge
            5 => (cx - (newWidth / 2), original.Top),                       // S: fix top edge
            3 => (original.Left, cy - (newHeight / 2)),                     // E: fix left edge
            _ => (original.Right - newWidth, cy - (newHeight / 2)),         // W (7): fix right edge
        };

        return new Rect2D(newLeft, newTop, newWidth, newHeight);
    }

    private void EnsureUndoCaptured()
    {
        if (!_gesture.UndoCaptured && _vm is not null)
        {
            _vm.CaptureUndo();
            _gesture.UndoCaptured = true;
        }
    }

    // Full overlay rebuild: tear down and recreate every selection/connector handle and reference
    // outline. Used when the SET of handles changes (selection, selected connector, reference set,
    // data context) or at the end of a gesture; an in-progress drag uses RepositionOverlay instead.
    private void RebuildOverlay()
    {
        _lastHandleZoom = Zoom;

        foreach (Rectangle handle in _handles)
        {
            Overlay.Children.Remove(handle);
        }

        _handles.Clear();

        foreach (Control handle in _connectorHandles)
        {
            Overlay.Children.Remove(handle);
        }

        _connectorHandles.Clear();

        // Reference outlines are rebuilt alongside the handles so they track zoom/selection too
        // (self-contained: clears its own list, and no-ops when there's no VM or reference).
        RebuildReferenceOutlines();

        if (_vm is null)
        {
            return;
        }

        // Resize handles trace the bounding box of every selected node — this is what marks the
        // selection (single or multi), so each selected shape is outlined regardless of its kind.
        // Dragging a handle to resize is still gated to a single selection (see HitTestHandle); with
        // 2+ selected the handles are purely a visual cue. The node's own border is thickened
        // separately by the view model as a reinforcing cue.
        double size = HandleScreenSize / Zoom;
        foreach (NodeViewModelBase node in _vm.SelectedNodes)
        {
            foreach (Point position in HandlePositions(node))
            {
                Rectangle handle = new()
                {
                    Width = size,
                    Height = size,
                    Fill = Brushes.White,
                    Stroke = SelectionAccentBrush,
                    StrokeThickness = 1d / Zoom,
                };
                Canvas.SetLeft(handle, position.X - (size / 2));
                Canvas.SetTop(handle, position.Y - (size / 2));
                Overlay.Children.Add(handle);
                _handles.Add(handle);
            }
        }

        if (_vm.SelectedConnector is { } connector)
        {
            DrawConnectorHandles(connector);
        }

        UpdateScrollBars();
    }

    // Lightweight overlay update for an in-progress drag or a zoom change: reposition (and resize) the
    // existing handle controls in place rather than recreating them. The control order here matches how
    // RebuildOverlay created them. If the handle set no longer matches the selection — a set change
    // should have routed through RebuildOverlay — this falls back to a full rebuild, so a missed trigger
    // degrades to the old (correct, slower) behaviour instead of mis-tracking.
    private void RepositionOverlay()
    {
        _lastHandleZoom = Zoom;

        if (_vm is null)
        {
            return;
        }

        double size = HandleScreenSize / Zoom;
        double stroke = 1d / Zoom;

        // Node resize handles: selected nodes × 8 positions, in creation order.
        int index = 0;
        foreach (NodeViewModelBase node in _vm.SelectedNodes)
        {
            foreach (Point position in HandlePositions(node))
            {
                if (index >= _handles.Count)
                {
                    RebuildOverlay();
                    return;
                }

                PlaceCenteredHandle(_handles[index], position.X, position.Y, size, stroke);
                index++;
            }
        }

        if (index != _handles.Count)
        {
            RebuildOverlay();
            return;
        }

        // Connector handles: [source endpoint, target endpoint, bend point 0, 1, …].
        if (_vm.SelectedConnector is { } connector)
        {
            IReadOnlyList<Point2D> waypoints = connector.Waypoints;
            if (_connectorHandles.Count != waypoints.Count + 2)
            {
                RebuildOverlay();
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
            RebuildOverlay();
            return;
        }

        if (!TryRepositionReferenceOutlines(size))
        {
            RebuildOverlay();
            return;
        }

        UpdateScrollBars();
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
    private static void PlaceEndpointHandle(Control handle, Point2D position, bool filled, double size, double stroke)
    {
        PlaceCenteredHandle(handle, position.X, position.Y, size, stroke);
        if (handle is Shape shape)
        {
            shape.Fill = filled ? SelectionAccentBrush : Brushes.White;
        }
    }

    // Marks the fixed alignment-reference shapes with a dashed amber box just outside each one — drawn on
    // the zoom-scaled overlay like the selection handles, but in a distinct colour and shown even when the
    // reference isn't selected. Rebuilt on every RebuildOverlay() pass so it tracks zoom/selection.
    private void RebuildReferenceOutlines()
    {
        foreach (Rectangle outline in _referenceOutlines)
        {
            Overlay.Children.Remove(outline);
        }

        _referenceOutlines.Clear();

        if (_vm is null)
        {
            return;
        }

        double inset = HandleScreenSize / Zoom / 2d;
        double thickness = 1.5d / Zoom;
        foreach (NodeViewModelBase node in _vm.ReferenceNodes)
        {
            Rect2D b = node.Model.Bounds;
            Rectangle outline = new()
            {
                Width = b.Width + (inset * 2d),
                Height = b.Height + (inset * 2d),
                Stroke = ReferenceAccentBrush,
                StrokeThickness = thickness,
                StrokeDashArray = new AvaloniaList<double> { 4d, 3d },
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(outline, b.Left - inset);
            Canvas.SetTop(outline, b.Top - inset);
            Overlay.Children.Add(outline);
            _referenceOutlines.Add(outline);
        }
    }

    // Repositions/resizes the existing reference outlines in place; returns false (→ caller rebuilds)
    // when the reference set no longer matches the outline controls.
    private bool TryRepositionReferenceOutlines(double size)
    {
        if (_vm is null)
        {
            return true;
        }

        double inset = size / 2d;
        double thickness = 1.5d / Zoom;
        int index = 0;
        foreach (NodeViewModelBase node in _vm.ReferenceNodes)
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
        double size = HandleScreenSize / Zoom;
        IBrush stroke = SelectionAccentBrush;

        AddEndpointHandle(connector.RouteStart, connector.SourceAnchored, size, stroke);
        AddEndpointHandle(connector.RouteEnd, connector.TargetAnchored, size, stroke);

        foreach (Point2D waypoint in connector.Waypoints)
        {
            Rectangle handle = new()
            {
                Width = size,
                Height = size,
                Fill = Brushes.White,
                Stroke = stroke,
                StrokeThickness = 1d / Zoom,
            };
            Canvas.SetLeft(handle, waypoint.X - (size / 2));
            Canvas.SetTop(handle, waypoint.Y - (size / 2));
            Overlay.Children.Add(handle);
            _connectorHandles.Add(handle);
        }
    }

    private void AddEndpointHandle(Point2D position, bool filled, double size, IBrush stroke)
    {
        Ellipse handle = new()
        {
            Width = size,
            Height = size,
            Fill = filled ? stroke : Brushes.White,
            Stroke = stroke,
            StrokeThickness = 1d / Zoom,
        };
        Canvas.SetLeft(handle, position.X - (size / 2));
        Canvas.SetTop(handle, position.Y - (size / 2));
        Overlay.Children.Add(handle);
        _connectorHandles.Add(handle);
    }

    private void StartMarquee(Point world)
    {
        _gesture.Marquee = new Rectangle
        {
            Fill = new SolidColorBrush(Color.FromArgb(40, 61, 126, 255)),
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
