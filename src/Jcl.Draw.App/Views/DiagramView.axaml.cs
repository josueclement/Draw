using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Jcl.Draw.App.ViewModels;
using Jcl.Draw.Diagramming.Geometry;
using Jcl.Draw.Model.Connectors;
using Jcl.Draw.Model.Primitives;

namespace Jcl.Draw.App.Views;

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
    }

    private const double HandleScreenSize = 10d;
    private const double MinShapeSize = 12d;

    private readonly List<Rectangle> _handles = new();
    private DiagramDocumentViewModel? _vm;
    private DragMode _mode = DragMode.None;
    private bool _undoCaptured;
    private Point _lastScreen;
    private Point _moveLastWorld;
    private Point _marqueeStartWorld;
    private bool _marqueeAdditive;
    private Rectangle? _marquee;
    private int _resizeHandle = -1;
    private ShapeNodeViewModel? _resizeTarget;
    private ShapeNodeViewModel? _connectSource;
    private Line? _connectPreview;

    public DiagramView()
    {
        InitializeComponent();

        Viewport.PointerPressed += OnPointerPressed;
        Viewport.PointerMoved += OnPointerMoved;
        Viewport.PointerReleased += OnPointerReleased;
        Viewport.PointerWheelChanged += OnPointerWheel;
        Viewport.DoubleTapped += OnDoubleTapped;
        KeyDown += OnKeyDown;
        DataContextChanged += OnDataContextChanged;
    }

    private double Zoom => _vm?.Zoom ?? 1d;

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
        UpdateHandles();
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

        if (point.Properties.IsMiddleButtonPressed || point.Properties.IsRightButtonPressed)
        {
            _mode = DragMode.Pan;
            _lastScreen = screen;
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
            ShapeNodeViewModel? from = HitTestNode(world);
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

        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        ShapeNodeViewModel? node = HitTestNode(world);
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
                    ShapeNodeViewModel? target = HitTestNode(ScreenToWorld(e.GetPosition(Viewport)));
                    if (_connectSource is not null && target is not null && !ReferenceEquals(target, _connectSource))
                    {
                        ToolboxViewModel? toolbox = GetToolbox();
                        RelationshipKind kind = toolbox?.SelectedConnector?.Kind ?? RelationshipKind.Association;
                        _vm.AddConnector(_connectSource.Id, target.Id, kind);
                        toolbox?.ActivateSelectTool();
                    }

                    EndConnectPreview();
                    break;
            }
        }

        _mode = DragMode.None;
        _undoCaptured = false;
        _resizeHandle = -1;
        _resizeTarget = null;
        _connectSource = null;
        e.Pointer.Capture(null);
        UpdateHandles();
    }

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
        ShapeNodeViewModel? node = HitTestNode(world);
        if (node is not null)
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

    private void EndEditing()
    {
        if (_vm is null)
        {
            return;
        }

        foreach (ShapeNodeViewModel node in _vm.Nodes.Where(n => n.IsEditing))
        {
            node.IsEditing = false;
        }
    }

    private ShapeNodeViewModel? HitTestNode(Point world)
    {
        Point2D p = new(world.X, world.Y);
        return _vm?.Nodes.LastOrDefault(n => n.Model.Bounds.Contains(p));
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

    private static Point[] HandlePositions(ShapeNodeViewModel node)
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

    private static Rect2D ResizeBounds(ShapeNodeViewModel node, int handle, Point world)
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
        double width = Math.Max(MinShapeSize, Math.Abs(r - l));
        double height = Math.Max(MinShapeSize, Math.Abs(bottom - t));
        return new Rect2D(left, top, width, height);
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

        if (_vm is null || _vm.SelectedNodes.Count() != 1)
        {
            return;
        }

        double size = HandleScreenSize / Zoom;
        IBrush stroke = new SolidColorBrush(Color.Parse("#3D7EFF"));
        foreach (Point position in HandlePositions(_vm.SelectedNodes.First()))
        {
            Rectangle handle = new()
            {
                Width = size,
                Height = size,
                Fill = Brushes.White,
                Stroke = stroke,
                StrokeThickness = 1d / Zoom,
            };
            Canvas.SetLeft(handle, position.X - (size / 2));
            Canvas.SetTop(handle, position.Y - (size / 2));
            Overlay.Children.Add(handle);
            _handles.Add(handle);
        }
    }

    private void StartMarquee(Point world)
    {
        _marquee = new Rectangle
        {
            Fill = new SolidColorBrush(Color.FromArgb(40, 61, 126, 255)),
            Stroke = new SolidColorBrush(Color.Parse("#3D7EFF")),
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

    private void StartConnectPreview(ShapeNodeViewModel from, Point world)
    {
        Point2D center = from.Model.Bounds.Center;
        _connectPreview = new Line
        {
            StartPoint = new Point(center.X, center.Y),
            EndPoint = world,
            Stroke = new SolidColorBrush(Color.Parse("#3D7EFF")),
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
