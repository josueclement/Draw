using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Draw.App.ViewModels;
using Draw.Diagramming.Geometry;
using Draw.Diagramming.Layout;
using Draw.Model.Connectors;
using Draw.Model.Nodes;
using Draw.Model.Primitives;

namespace Draw.App.Views;

public partial class DiagramView : UserControl
{
    private enum DragMode
    {
        None,
        Move,
        Marquee,
        Pan,
        Resize,
        Connect,
        EndpointMove,
        WaypointMove,
        LabelMove,
    }

    private enum ConnectorHitKind
    {
        None,
        Endpoint,
        Waypoint,
        Label,
    }

    private readonly record struct ConnectorHit(ConnectorHitKind Kind, bool IsSource, int BendIndex, ConnectorLabelKind Label);

    private const double HandleScreenSize = 10d;

    // Selection accent shared by the resize handles, marquee and connect-preview.
    private static readonly Color SelectionAccentColor = Color.Parse("#3D7EFF");
    private static readonly SolidColorBrush SelectionAccentBrush = new(SelectionAccentColor);

    // A right-button press that travels less than this (screen px, squared) before release is treated as
    // a click that opens the arrange context menu rather than a pan drag.
    private const double ContextClickThresholdSquared = 16d;

    private readonly List<Rectangle> _handles = new();
    private readonly List<Control> _connectorHandles = new();
    private DiagramDocumentViewModel? _vm;
    private DragMode _mode = DragMode.None;
    private bool _undoCaptured;
    private Point _lastScreen;
    private Point _panStartScreen;
    private Point _moveLastWorld;
    private Point _marqueeStartWorld;
    private bool _marqueeAdditive;
    private Rectangle? _marquee;
    private int _resizeHandle = -1;
    private NodeViewModelBase? _resizeTarget;
    private NodeViewModelBase? _connectSource;
    private Line? _connectPreview;
    private ToolboxViewModel? _toolbox;

    // Connector-edit gesture state (endpoint anchor / waypoint / label drags on the selected connector).
    private ConnectorViewModel? _editConnector;
    private bool _editSourceEndpoint;
    private int _editBendIndex = -1;
    private ConnectorLabelKind _editLabelKind;
    private Point _labelGrabDelta;

    public DiagramView()
    {
        InitializeComponent();

        Viewport.PointerPressed += OnPointerPressed;
        Viewport.PointerMoved += OnPointerMoved;
        Viewport.PointerReleased += OnPointerReleased;
        Viewport.PointerWheelChanged += OnPointerWheel;
        Viewport.DoubleTapped += OnDoubleTapped;
        Viewport.SizeChanged += (_, _) =>
        {
            UpdateGridBounds();
            PushViewportSize();
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
        UpdateHandles();
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

    private void OnVmSelectionChanged(object? sender, EventArgs e) => UpdateHandles();

    private Point ScreenToWorld(Point screen)
        => new((screen.X - (_vm?.PanX ?? 0)) / Zoom, (screen.Y - (_vm?.PanY ?? 0)) / Zoom);

    private void UpdateTransform()
    {
        double zoom = Zoom;
        World.RenderTransform = new MatrixTransform(new Matrix(zoom, 0, 0, zoom, _vm?.PanX ?? 0, _vm?.PanY ?? 0));
        UpdateGridBounds();
        UpdateHandles();
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
            _mode = DragMode.Pan;
            _lastScreen = screen;
            _panStartScreen = screen;
            e.Pointer.Capture(Viewport);
            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        int handle = HitTestHandle(world);
        if (handle >= 0 && _vm.SelectedNodes.Count() == 1)
        {
            _mode = DragMode.Resize;
            _resizeHandle = handle;
            _resizeTarget = _vm.SelectedNodes.First();
            _undoCaptured = false;
            e.Pointer.Capture(Viewport);
            return;
        }

        ToolboxViewModel? toolbox = GetToolbox();

        // Connector mode: drag from a source node to a target node.
        if (toolbox is { IsConnectorMode: true })
        {
            NodeViewModelBase? from = HitTestNode(world);
            if (from is not null)
            {
                _connectSource = from;
                _mode = DragMode.Connect;
                StartConnectPreview(from, world);
                e.Pointer.Capture(Viewport);
            }

            return;
        }

        // Shape placement.
        if (toolbox?.SelectedShape is { } tool)
        {
            _vm.AddShape(tool.Kind, new Point2D(world.X, world.Y));
            toolbox.ActivateSelectTool();
            return;
        }

        // Class-node placement.
        if (toolbox?.SelectedClassNode is { } classTool)
        {
            _vm.AddClassNode(classTool.Kind, new Point2D(world.X, world.Y));
            toolbox.ActivateSelectTool();
            return;
        }

        // Use-case-node placement.
        if (toolbox?.SelectedUseCaseNode is { } useCaseTool)
        {
            _vm.AddUseCaseNode(useCaseTool.Kind, new Point2D(world.X, world.Y));
            toolbox.ActivateSelectTool();
            return;
        }

        // ER table placement.
        if (toolbox is { SelectedEntity: not null })
        {
            _vm.AddEntityNode(new Point2D(world.X, world.Y));
            toolbox.ActivateSelectTool();
            return;
        }

        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        // Editing the selected connector's handles takes precedence over node hit-testing, because
        // endpoint handles sit on the shape boundaries and would otherwise be stolen by the node.
        if (_vm.SelectedConnector is { } selected)
        {
            ConnectorHit hit = HitConnector(selected, world);
            if (hit.Kind != ConnectorHitKind.None)
            {
                BeginConnectorEdit(selected, hit, world, alt);
                e.Pointer.Capture(Viewport);
                return;
            }
        }

        NodeViewModelBase? node = HitTestNode(world);
        if (node is not null)
        {
            if (ctrl)
            {
                _vm.ToggleSelect(node);
            }
            else if (!node.IsSelected)
            {
                _vm.SelectOnly(node);
            }

            _mode = DragMode.Move;
            _moveLastWorld = world;
            _undoCaptured = false;
        }
        else
        {
            ConnectorViewModel? connector = _vm.HitTestConnector(new Point2D(world.X, world.Y), 6d / Zoom);
            if (connector is not null)
            {
                // Ctrl+click on a straight/orthogonal connector splits it: insert a bend point at the
                // click and immediately drag it (one undo step covers the add + reposition).
                if (ctrl && connector.SupportsWaypoints)
                {
                    _vm.CaptureUndo();
                    _vm.SelectConnector(connector);
                    _editConnector = connector;
                    _editBendIndex = connector.InsertBendPointAt(new Point2D(world.X, world.Y));
                    _mode = DragMode.WaypointMove;
                    _undoCaptured = true;
                    UpdateHandles();
                    e.Pointer.Capture(Viewport);
                    return;
                }

                _vm.SelectConnector(connector);
                e.Pointer.Capture(Viewport);
                return;
            }

            _mode = DragMode.Marquee;
            _marqueeStartWorld = world;
            _marqueeAdditive = ctrl;
            if (!ctrl)
            {
                _vm.ClearSelection();
            }

            StartMarquee(world);
        }

        e.Pointer.Capture(Viewport);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_vm is null || _mode == DragMode.None)
        {
            return;
        }

        Point screen = e.GetPosition(Viewport);
        Point world = ScreenToWorld(screen);

        switch (_mode)
        {
            case DragMode.Pan:
                _vm.PanX += screen.X - _lastScreen.X;
                _vm.PanY += screen.Y - _lastScreen.Y;
                _lastScreen = screen;
                break;

            case DragMode.Move:
                EnsureUndoCaptured();
                _vm.MoveSelectedBy(world.X - _moveLastWorld.X, world.Y - _moveLastWorld.Y);
                _moveLastWorld = world;
                UpdateHandles();
                break;

            case DragMode.Resize when _resizeTarget is not null:
                EnsureUndoCaptured();
                _vm.SetNodeBounds(_resizeTarget, ResizeBounds(_resizeTarget, _resizeHandle, world));
                UpdateHandles();
                break;

            case DragMode.Marquee:
                UpdateMarquee(world);
                break;

            case DragMode.Connect:
                UpdateConnectPreview(world);
                break;

            case DragMode.EndpointMove when _editConnector is not null:
                EnsureUndoCaptured();
                SetEndpointAnchor(_editConnector, _editSourceEndpoint, world);
                UpdateHandles();
                break;

            case DragMode.WaypointMove when _editConnector is not null:
                EnsureUndoCaptured();
                _editConnector.MoveBendPoint(_editBendIndex, new Point2D(world.X, world.Y));
                UpdateHandles();
                break;

            case DragMode.LabelMove when _editConnector is not null:
                EnsureUndoCaptured();
                Point2D natural = _editConnector.NaturalLabelAnchor(_editLabelKind);
                _editConnector.SetLabelOffset(
                    _editLabelKind,
                    new Point2D((world.X + _labelGrabDelta.X) - natural.X, (world.Y + _labelGrabDelta.Y) - natural.Y));
                UpdateHandles();
                break;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_vm is not null)
        {
            switch (_mode)
            {
                case DragMode.Move:
                    _vm.SnapSelectionToGrid();
                    break;

                case DragMode.Resize when _resizeTarget is not null && _vm.SnapEnabled:
                    Rect2D snapped = _resizeTarget.Model.Bounds.EdgesSnappedToGrid(_vm.GridSize);
                    _vm.SetNodeBounds(_resizeTarget, snapped);
                    break;

                case DragMode.Marquee:
                    Point world = ScreenToWorld(e.GetPosition(Viewport));
                    Rect2D rect = Rect2D.FromPoints(
                        new Point2D(_marqueeStartWorld.X, _marqueeStartWorld.Y),
                        new Point2D(world.X, world.Y));
                    _vm.SelectInRect(rect, _marqueeAdditive);
                    EndMarquee();
                    break;

                case DragMode.Connect:
                    NodeViewModelBase? target = HitTestNode(ScreenToWorld(e.GetPosition(Viewport)));
                    if (_connectSource is not null && target is not null && !ReferenceEquals(target, _connectSource))
                    {
                        ToolboxViewModel? toolbox = GetToolbox();
                        RelationshipKind kind = toolbox?.SelectedConnector?.Kind ?? RelationshipKind.Association;
                        _vm.AddConnector(_connectSource.Id, target.Id, kind);
                        toolbox?.ActivateSelectTool();
                    }

                    EndConnectPreview();
                    break;

                case DragMode.EndpointMove:
                    _vm.MarkModified();
                    break;

                case DragMode.WaypointMove when _editConnector is not null:
                    if (_vm.SnapEnabled)
                    {
                        _editConnector.SnapBendPointToGrid(_editBendIndex, _vm.GridSize);
                    }

                    _vm.MarkModified();
                    break;

                case DragMode.LabelMove when _editConnector is not null:
                    if (_vm.SnapEnabled)
                    {
                        _editConnector.SnapLabelToGrid(_editLabelKind, _vm.GridSize);
                    }

                    _vm.MarkModified();
                    break;

                case DragMode.Pan:
                    MaybeShowArrangeMenu(e);
                    break;
            }
        }

        _mode = DragMode.None;
        _undoCaptured = false;
        _resizeHandle = -1;
        _resizeTarget = null;
        _connectSource = null;
        _editConnector = null;
        _editBendIndex = -1;
        e.Pointer.Capture(null);
        UpdateHandles();
    }

    // Right-clicking the canvas (a click, not a pan drag) with at least one selected shape opens the
    // arrange menu. Align/Distribute need >=2 / >=3 and grey themselves out below that; Space connections
    // works on a single shape. Right-dragging still pans; the middle button never opens the menu.
    private void MaybeShowArrangeMenu(PointerReleasedEventArgs e)
    {
        if (_vm is null || e.InitialPressMouseButton != MouseButton.Right || !_vm.SelectedNodes.Any())
        {
            return;
        }

        Point release = e.GetPosition(Viewport);
        double dx = release.X - _panStartScreen.X;
        double dy = release.Y - _panStartScreen.Y;
        if (((dx * dx) + (dy * dy)) > ContextClickThresholdSquared)
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
        MenuItem align = new() { Header = "Align" };
        align.Items.Add(ArrangeItem("Align left", vm.AlignCommand, AlignmentMode.Left));
        align.Items.Add(ArrangeItem("Align center", vm.AlignCommand, AlignmentMode.CenterHorizontal));
        align.Items.Add(ArrangeItem("Align right", vm.AlignCommand, AlignmentMode.Right));
        align.Items.Add(new Separator());
        align.Items.Add(ArrangeItem("Align top", vm.AlignCommand, AlignmentMode.Top));
        align.Items.Add(ArrangeItem("Align middle", vm.AlignCommand, AlignmentMode.CenterVertical));
        align.Items.Add(ArrangeItem("Align bottom", vm.AlignCommand, AlignmentMode.Bottom));

        MenuItem order = new() { Header = "Order" };
        order.Items.Add(ArrangeItem("Bring to front", vm.OrderCommand, ZOrderOperation.BringToFront));
        order.Items.Add(ArrangeItem("Bring forward", vm.OrderCommand, ZOrderOperation.BringForward));
        order.Items.Add(ArrangeItem("Send backward", vm.OrderCommand, ZOrderOperation.SendBackward));
        order.Items.Add(ArrangeItem("Send to back", vm.OrderCommand, ZOrderOperation.SendToBack));

        ContextMenu menu = new();
        menu.Items.Add(align);
        menu.Items.Add(order);
        menu.Items.Add(new Separator());
        menu.Items.Add(ArrangeItem("Distribute horizontally", vm.DistributeCommand, DistributionMode.Horizontal));
        menu.Items.Add(ArrangeItem("Distribute vertically", vm.DistributeCommand, DistributionMode.Vertical));
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "Space connections", Command = vm.SpaceConnectionsCommand });
        menu.Items.Add(new MenuItem { Header = "Merge connections", Command = vm.MergeConnectionsCommand });
        return menu;
    }

    private static MenuItem ArrangeItem<T>(string header, RelayCommand<T> command, T parameter)
        => new() { Header = header, Command = command, CommandParameter = parameter };

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
        }
    }

    /// <summary>The visual rendered when exporting to PNG / clipboard (the current viewport).</summary>
    public Control ExportTarget => Viewport;

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Don't steal keys while editing a node's text.
        if (e.Source is TextBox)
        {
            if (e.Key == Key.Escape)
            {
                EndEditing();
            }

            return;
        }

        // Clipboard shortcuts are handled here (not as window key bindings) so they only act on the
        // diagram when no text editor has focus — the TextBox guard above lets them edit text normally.
        if (_vm is not null && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Key.C:
                    _ = _vm.CopySelectionAsync();
                    e.Handled = true;
                    return;
                case Key.X:
                    _ = _vm.CutSelectionAsync();
                    e.Handled = true;
                    return;
                case Key.V:
                    _ = _vm.PasteAsync();
                    e.Handled = true;
                    return;
                case Key.D:
                    _vm.DuplicateSelection();
                    e.Handled = true;
                    return;
            }
        }

        if (e.Key == Key.Delete)
        {
            _vm?.DeleteSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            EndEditing();
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
        return _vm?.Nodes.Where(n => n.Model.Bounds.Contains(p)).MaxBy(n => n.Model.ZIndex);
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
        if (!_undoCaptured && _vm is not null)
        {
            _vm.CaptureUndo();
            _undoCaptured = true;
        }
    }

    private void UpdateHandles()
    {
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

    // Code-based hit-testing (the connector layer is IsHitTestVisible=false). Endpoints and bend
    // points use the precise handle tolerance; labels use an estimated text box and are lowest
    // priority so they don't steal an endpoint grab.
    private ConnectorHit HitConnector(ConnectorViewModel connector, Point world)
    {
        double tolerance = HandleScreenSize / Zoom;
        Point2D w = new(world.X, world.Y);

        if (Within(connector.RouteStart, w, tolerance))
        {
            return new ConnectorHit(ConnectorHitKind.Endpoint, true, -1, default);
        }

        if (Within(connector.RouteEnd, w, tolerance))
        {
            return new ConnectorHit(ConnectorHitKind.Endpoint, false, -1, default);
        }

        IReadOnlyList<Point2D> waypoints = connector.Waypoints;
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (Within(waypoints[i], w, tolerance))
            {
                return new ConnectorHit(ConnectorHitKind.Waypoint, false, i, default);
            }
        }

        if (connector.HasCenterLabel && LabelHit(connector, ConnectorLabelKind.Center, connector.CenterLabelText, world))
        {
            return new ConnectorHit(ConnectorHitKind.Label, false, -1, ConnectorLabelKind.Center);
        }

        if (connector.HasSourceLabel && LabelHit(connector, ConnectorLabelKind.Source, connector.SourceLabelText, world))
        {
            return new ConnectorHit(ConnectorHitKind.Label, false, -1, ConnectorLabelKind.Source);
        }

        if (connector.HasTargetLabel && LabelHit(connector, ConnectorLabelKind.Target, connector.TargetLabelText, world))
        {
            return new ConnectorHit(ConnectorHitKind.Label, false, -1, ConnectorLabelKind.Target);
        }

        return new ConnectorHit(ConnectorHitKind.None, false, -1, default);
    }

    private static bool Within(Point2D a, Point2D b, double tolerance)
        => Math.Abs(a.X - b.X) <= tolerance && Math.Abs(a.Y - b.Y) <= tolerance;

    private static bool LabelHit(ConnectorViewModel connector, ConnectorLabelKind kind, string? text, Point world)
    {
        Point2D topLeft = connector.LabelDisplay(kind);
        double width = Math.Max(8d, (text?.Length ?? 0) * 7d); // FontSize 11 estimate
        const double height = 16d;
        return world.X >= topLeft.X - 2d && world.X <= topLeft.X + width + 2d
            && world.Y >= topLeft.Y - 2d && world.Y <= topLeft.Y + height + 2d;
    }

    private void BeginConnectorEdit(ConnectorViewModel connector, ConnectorHit hit, Point world, bool alt)
    {
        if (_vm is null)
        {
            return;
        }

        _editConnector = connector;

        // Alt = remove/reset (a discrete action, not a drag), consistent across all three handles.
        if (alt)
        {
            _vm.CaptureUndo();
            switch (hit.Kind)
            {
                case ConnectorHitKind.Endpoint when hit.IsSource:
                    connector.SetSourceAnchor(null);
                    break;
                case ConnectorHitKind.Endpoint:
                    connector.SetTargetAnchor(null);
                    break;
                case ConnectorHitKind.Waypoint:
                    connector.RemoveBendPoint(hit.BendIndex);
                    break;
                case ConnectorHitKind.Label:
                    connector.SetLabelOffset(hit.Label, null);
                    break;
            }

            _vm.MarkModified();
            _mode = DragMode.None;
            _editConnector = null;
            UpdateHandles();
            return;
        }

        switch (hit.Kind)
        {
            case ConnectorHitKind.Endpoint:
                _editSourceEndpoint = hit.IsSource;
                _mode = DragMode.EndpointMove;
                break;
            case ConnectorHitKind.Waypoint:
                _editBendIndex = hit.BendIndex;
                _mode = DragMode.WaypointMove;
                break;
            case ConnectorHitKind.Label:
                _editLabelKind = hit.Label;
                Point2D display = connector.LabelDisplay(hit.Label);
                _labelGrabDelta = new Point(display.X - world.X, display.Y - world.Y);
                _mode = DragMode.LabelMove;
                break;
        }

        _undoCaptured = false;
    }

    private void SetEndpointAnchor(ConnectorViewModel connector, bool source, Point world)
    {
        Rect2D bounds = (source ? connector.Source : connector.Target).Bounds;
        double u = bounds.Width <= 0 ? 0.5 : Math.Clamp((world.X - bounds.X) / bounds.Width, 0d, 1d);
        double v = bounds.Height <= 0 ? 0.5 : Math.Clamp((world.Y - bounds.Y) / bounds.Height, 0d, 1d);
        Point2D relative = new(u, v);
        if (source)
        {
            connector.SetSourceAnchor(relative);
        }
        else
        {
            connector.SetTargetAnchor(relative);
        }
    }

    private void StartMarquee(Point world)
    {
        _marquee = new Rectangle
        {
            Fill = new SolidColorBrush(Color.FromArgb(40, 61, 126, 255)),
            Stroke = SelectionAccentBrush,
            StrokeThickness = 1d / Zoom,
        };
        Canvas.SetLeft(_marquee, world.X);
        Canvas.SetTop(_marquee, world.Y);
        Overlay.Children.Add(_marquee);
    }

    private void UpdateMarquee(Point world)
    {
        if (_marquee is null)
        {
            return;
        }

        double left = Math.Min(world.X, _marqueeStartWorld.X);
        double top = Math.Min(world.Y, _marqueeStartWorld.Y);
        Canvas.SetLeft(_marquee, left);
        Canvas.SetTop(_marquee, top);
        _marquee.Width = Math.Abs(world.X - _marqueeStartWorld.X);
        _marquee.Height = Math.Abs(world.Y - _marqueeStartWorld.Y);
    }

    private void EndMarquee()
    {
        if (_marquee is not null)
        {
            Overlay.Children.Remove(_marquee);
            _marquee = null;
        }
    }

    private void StartConnectPreview(NodeViewModelBase from, Point world)
    {
        Point2D center = from.Model.Bounds.Center;
        _connectPreview = new Line
        {
            StartPoint = new Point(center.X, center.Y),
            EndPoint = world,
            Stroke = SelectionAccentBrush,
            StrokeThickness = 1.5d / Zoom,
            StrokeDashArray = new AvaloniaList<double> { 4, 2 },
            IsHitTestVisible = false,
        };
        Overlay.Children.Add(_connectPreview);
    }

    private void UpdateConnectPreview(Point world)
    {
        if (_connectPreview is not null)
        {
            _connectPreview.EndPoint = world;
        }
    }

    private void EndConnectPreview()
    {
        if (_connectPreview is not null)
        {
            Overlay.Children.Remove(_connectPreview);
            _connectPreview = null;
        }

        _connectSource = null;
    }

    private ToolboxViewModel? GetToolbox()
        => TopLevel.GetTopLevel(this)?.DataContext as ShellViewModel is { } shell ? shell.Toolbox : null;
}
