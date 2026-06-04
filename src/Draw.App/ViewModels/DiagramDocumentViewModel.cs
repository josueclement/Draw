using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Draw.App.Configuration;
using Draw.App.Services;
using Draw.Diagramming.Geometry;
using Draw.Diagramming.Layout;
using Draw.Diagramming.Routing;
using Draw.Diagramming.Uml;
using Draw.Diagramming.Undo;
using Draw.Model.Connectors;
using Draw.Model.Documents;
using Draw.Model.Nodes;
using Draw.Model.Primitives;
using Draw.Model.Serialization;

namespace Draw.App.ViewModels;

/// <summary>Editor state and mutating operations for one open diagram (one tab).</summary>
public sealed class DiagramDocumentViewModel : ViewModelBase, INodeEditContext, IDisposable
{
    private readonly IUndoService _undo;
    private readonly IConnectorRouter _router;
    private readonly IDocumentSerializer _serializer;
    private readonly EditorOptions _options;
    private readonly IThemeService _theme;
    private DiagramDocument _document;
    private string _cleanSnapshot;

    // Ribbon View-tab zoom step + bounds; bounds mirror the Ctrl+wheel clamp in DiagramView.
    private const double ZoomStep = 1.2d;
    private const double MinZoom = 0.1d;
    private const double MaxZoom = 8d;

    public DiagramDocumentViewModel(
        DiagramDocument document,
        IUndoService undo,
        IConnectorRouter router,
        IDocumentSerializer serializer,
        EditorOptions options,
        IThemeService theme,
        string? filePath)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _undo = undo ?? throw new ArgumentNullException(nameof(undo));
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        FilePath = filePath;
        _undo.StateChanged += (_, _) => RaiseUndoState();
        _theme.ThemeChanged += OnThemeChanged;
        // Construct the selection-gated commands before RebuildNodes(): it raises a selection-changed
        // notification that calls NotifyCanExecuteChanged() on both, so they must already exist.
        AlignCommand = new RelayCommand<AlignmentMode>(
            mode => { if (mode is { } m) AlignSelected(m); },
            _ => CanAlignSelection);
        DistributeCommand = new RelayCommand<DistributionMode>(
            mode => { if (mode is { } m) DistributeSelected(m); },
            _ => CanDistributeSelection);
        SpaceConnectionsCommand = new RelayCommand(SpaceSelectedConnections, () => CanSpaceConnections);

        RebuildNodes();
        RebuildConnectors();
        _cleanSnapshot = _serializer.Serialize(_document);

        ZoomInCommand = new RelayCommand(() => Zoom = Math.Clamp(Zoom * ZoomStep, MinZoom, MaxZoom));
        ZoomOutCommand = new RelayCommand(() => Zoom = Math.Clamp(Zoom / ZoomStep, MinZoom, MaxZoom));
        ZoomResetCommand = new RelayCommand(() =>
        {
            Zoom = 1d;
            PanX = 0d;
            PanY = 0d;
        });
    }

    public ObservableCollection<NodeViewModelBase> Nodes { get; } = new();

    public ObservableCollection<ConnectorViewModel> Connectors { get; } = new();

    public DiagramDocument Document => _document;

    public DiagramType DiagramType => _document.DiagramType;

    public double GridSize => _options.GridSize;

    public bool SnapEnabled => _options.SnapToGrid;

    public string? FilePath
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public bool IsModified
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public string DisplayName
    {
        get
        {
            string name = FilePath is null ? "Untitled" : Path.GetFileName(FilePath);
            return IsModified ? name + " *" : name;
        }
    }

    public double Zoom
    {
        get;
        set => SetProperty(ref field, value);
    } = 1d;

    public RelayCommand ZoomInCommand { get; }

    public RelayCommand ZoomOutCommand { get; }

    public RelayCommand ZoomResetCommand { get; }

    public RelayCommand<AlignmentMode> AlignCommand { get; }

    public RelayCommand<DistributionMode> DistributeCommand { get; }

    public RelayCommand SpaceConnectionsCommand { get; }

    public double PanX
    {
        get;
        set => SetProperty(ref field, value);
    }

    public double PanY
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool CanUndo => _undo.CanUndo;

    public bool CanRedo => _undo.CanRedo;

    public bool HasSelection => Nodes.Any(n => n.IsSelected) || SelectedConnector is not null;

    /// <summary>Alignment needs at least two shapes to have a common edge/center to line up on.</summary>
    public bool CanAlignSelection => SelectedNodes.Count() >= 2;

    /// <summary>Distribution needs at least three shapes (two anchors plus something to space between).</summary>
    public bool CanDistributeSelection => SelectedNodes.Count() >= 3;

    /// <summary>Connector spacing operates on every connector attached to the selected shape(s).</summary>
    public bool CanSpaceConnections => SelectedNodes.Any();

    public bool HasConnectorSelection => SelectedConnector is not null;

    public ConnectorViewModel? SelectedConnector
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasConnectorSelection));
            }
        }
    }

    public IEnumerable<NodeViewModelBase> SelectedNodes => Nodes.Where(n => n.IsSelected);

    public event EventHandler? UndoStateChanged;

    public event EventHandler? SelectionChanged;

    public void CaptureUndo() => _undo.Capture(_document);

    public ShapeNodeViewModel AddShape(ShapeKind kind, Point2D center)
    {
        CaptureUndo();

        double w = _options.DefaultShapeWidth;
        double h = _options.DefaultShapeHeight;
        Rect2D bounds = new(center.X - (w / 2), center.Y - (h / 2), w, h);
        if (SnapEnabled)
        {
            bounds = bounds.PositionSnappedToGrid(GridSize);
        }

        ShapeNode node = new()
        {
            Kind = kind,
            Bounds = bounds,
            Style = _document.DefaultShapeStyle.Clone(),
            ZIndex = NextZIndex(),
        };

        _document.Nodes.Add(node);
        ShapeNodeViewModel vm = new(node, _theme);
        Nodes.Add(vm);
        SelectOnly(vm);
        MarkModified();
        return vm;
    }

    private const double ClassNodeDefaultWidth = 170d;
    private const double ClassNodeDefaultHeight = 110d;

    public ClassNodeViewModel AddClassNode(ClassNodeKind kind, Point2D center)
    {
        CaptureUndo();

        double w = ClassNodeDefaultWidth;
        double h = ClassNodeDefaultHeight;
        Rect2D bounds = new(center.X - (w / 2), center.Y - (h / 2), w, h);
        if (SnapEnabled)
        {
            bounds = bounds.PositionSnappedToGrid(GridSize);
        }

        ClassNode node = new()
        {
            Kind = kind,
            Name = DefaultClassName(kind),
            Bounds = bounds,
            Style = _document.DefaultShapeStyle.Clone(),
            ZIndex = NextZIndex(),
        };

        _document.Nodes.Add(node);
        ClassNodeViewModel vm = new(node, this, _theme);
        Nodes.Add(vm);
        SelectOnly(vm);
        MarkModified();
        return vm;
    }

    private static string DefaultClassName(ClassNodeKind kind) => kind switch
    {
        ClassNodeKind.Interface => "Interface",
        ClassNodeKind.Enum => "Enumeration",
        _ => "Class",
    };

    private const double ActorDefaultWidth = 48d;
    private const double ActorDefaultHeight = 84d;
    private const double UseCaseDefaultWidth = 130d;
    private const double UseCaseDefaultHeight = 72d;
    private const double BoundaryDefaultWidth = 320d;
    private const double BoundaryDefaultHeight = 220d;

    public NodeViewModelBase AddUseCaseNode(UseCaseNodeKind kind, Point2D center)
    {
        CaptureUndo();

        (double w, double h) = kind switch
        {
            UseCaseNodeKind.Actor => (ActorDefaultWidth, ActorDefaultHeight),
            UseCaseNodeKind.SystemBoundary => (BoundaryDefaultWidth, BoundaryDefaultHeight),
            _ => (UseCaseDefaultWidth, UseCaseDefaultHeight),
        };

        Rect2D bounds = new(center.X - (w / 2), center.Y - (h / 2), w, h);
        if (SnapEnabled)
        {
            bounds = bounds.PositionSnappedToGrid(GridSize);
        }

        NodeBase node = kind switch
        {
            UseCaseNodeKind.Actor => new ActorNode
            {
                Name = "Actor", Bounds = bounds, Style = _document.DefaultShapeStyle.Clone(), ZIndex = NextZIndex(),
            },
            UseCaseNodeKind.SystemBoundary => new SystemBoundaryNode
            {
                Title = "System", Bounds = bounds, Style = _document.DefaultShapeStyle.Clone(), ZIndex = LowestZIndex() - 1,
            },
            _ => new UseCaseNode
            {
                Text = "Use case", Bounds = bounds, Style = _document.DefaultShapeStyle.Clone(), ZIndex = NextZIndex(),
            },
        };

        _document.Nodes.Add(node);
        NodeViewModelBase vm = CreateNodeViewModel(node);

        // A boundary renders behind everything: it gets the lowest z-index AND goes to the
        // front of the (insertion-ordered) collection so it draws first even before a rebuild.
        if (node is SystemBoundaryNode)
        {
            Nodes.Insert(0, vm);
        }
        else
        {
            Nodes.Add(vm);
        }

        SelectOnly(vm);
        MarkModified();
        return vm;
    }

    private int LowestZIndex() => _document.Nodes.Count == 0 ? 0 : _document.Nodes.Min(n => n.ZIndex);

    public ConnectorViewModel? AddConnector(Guid sourceId, Guid targetId, RelationshipKind kind)
    {
        if (sourceId == targetId)
        {
            return null;
        }

        NodeViewModelBase? source = FindNode(sourceId);
        NodeViewModelBase? target = FindNode(targetId);
        if (source is null || target is null)
        {
            return null;
        }

        CaptureUndo();
        Connector connector = new()
        {
            SourceNodeId = sourceId,
            TargetNodeId = targetId,
            Kind = kind,
            Route = RouteStyle.Straight,
        };
        _document.Connectors.Add(connector);
        ConnectorViewModel vm = new(connector, source, target, _router);
        Connectors.Add(vm);
        SelectConnector(vm);
        MarkModified();
        return vm;
    }

    public void DeleteSelected()
    {
        List<NodeViewModelBase> selectedNodes = SelectedNodes.ToList();
        ConnectorViewModel? selectedConnector = SelectedConnector;
        if (selectedNodes.Count == 0 && selectedConnector is null)
        {
            return;
        }

        CaptureUndo();

        if (selectedNodes.Count > 0)
        {
            HashSet<Guid> removedIds = selectedNodes.Select(n => n.Id).ToHashSet();
            foreach (NodeViewModelBase vm in selectedNodes)
            {
                _document.Nodes.Remove(vm.Model);
                Nodes.Remove(vm);
            }

            _document.Connectors.RemoveAll(c => removedIds.Contains(c.SourceNodeId) || removedIds.Contains(c.TargetNodeId));
            RebuildConnectors();
        }
        else if (selectedConnector is not null)
        {
            _document.Connectors.Remove(selectedConnector.Model);
            selectedConnector.Detach();
            Connectors.Remove(selectedConnector);
            SelectedConnector = null;
        }

        MarkModified();
        RaiseSelectionChanged();
    }

    public void MoveSelectedBy(double dx, double dy)
    {
        foreach (NodeViewModelBase vm in SelectedNodes)
        {
            vm.X += dx;
            vm.Y += dy;
        }

        MarkModified();
    }

    public void SnapSelectionToGrid()
    {
        if (!SnapEnabled)
        {
            return;
        }

        foreach (NodeViewModelBase vm in SelectedNodes)
        {
            Rect2D snapped = vm.Model.Bounds.PositionSnappedToGrid(GridSize);
            vm.X = snapped.X;
            vm.Y = snapped.Y;
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
        List<NodeViewModelBase> selected = SelectedNodes.ToList();
        if (selected.Count < minimum)
        {
            return;
        }

        CaptureUndo();
        IReadOnlyList<Rect2D> result = arrange(selected.Select(n => n.Model.Bounds).ToList());
        for (int i = 0; i < selected.Count; i++)
        {
            selected[i].X = result[i].X;
            selected[i].Y = result[i].Y;
        }

        MarkModified();
    }

    /// <summary>One connector endpoint that touches a shape being spaced.</summary>
    private readonly record struct ConnectorEnd(ConnectorViewModel Connector, bool IsSource, double Fraction);

    /// <summary>
    /// Evenly spreads the connectors attached to the selected shape(s) along each edge they touch
    /// (one undo step). For every selected shape, the connector ends on a given bounding-box side are
    /// kept in their current order and re-pinned at equal gaps; sides with a single connection are left
    /// alone. Reads the current routes before mutating, so each end is classified by where it lands now.
    /// </summary>
    public void SpaceSelectedConnections()
    {
        HashSet<Guid> selectedIds = SelectedNodes.Select(n => n.Id).ToHashSet();
        if (selectedIds.Count == 0)
        {
            return;
        }

        Dictionary<(NodeViewModelBase Node, BoxSide Side), List<ConnectorEnd>> groups = new();

        void Collect(ConnectorViewModel connector, bool isSource, NodeViewModelBase node, Point2D point)
        {
            BoxSide side = ConnectionDistributor.ClassifySide(node.Bounds, point);
            double fraction = ConnectionDistributor.FractionAlong(side, node.Bounds, point);
            if (!groups.TryGetValue((node, side), out List<ConnectorEnd>? ends))
            {
                ends = new List<ConnectorEnd>();
                groups[(node, side)] = ends;
            }

            ends.Add(new ConnectorEnd(connector, isSource, fraction));
        }

        foreach (ConnectorViewModel connector in Connectors)
        {
            if (selectedIds.Contains(connector.Source.Id))
            {
                Collect(connector, isSource: true, connector.Source, connector.RouteStart);
            }

            if (selectedIds.Contains(connector.Target.Id))
            {
                Collect(connector, isSource: false, connector.Target, connector.RouteEnd);
            }
        }

        bool anySpaced = false;
        foreach (KeyValuePair<(NodeViewModelBase Node, BoxSide Side), List<ConnectorEnd>> group in groups)
        {
            List<ConnectorEnd> ends = group.Value;
            if (ends.Count < 2)
            {
                continue;
            }

            // Capture only once we know there is something to do, so a no-op adds no undo entry.
            if (!anySpaced)
            {
                CaptureUndo();
                anySpaced = true;
            }

            ends.Sort((a, b) => a.Fraction.CompareTo(b.Fraction));
            for (int i = 0; i < ends.Count; i++)
            {
                Point2D anchor = ConnectionDistributor.EvenAnchor(group.Key.Side, i, ends.Count);
                if (ends[i].IsSource)
                {
                    ends[i].Connector.SetSourceAnchor(anchor);
                }
                else
                {
                    ends[i].Connector.SetTargetAnchor(anchor);
                }
            }
        }

        if (anySpaced)
        {
            MarkModified();
        }
    }

    public void SetNodeBounds(NodeViewModelBase vm, Rect2D bounds)
    {
        vm.X = bounds.X;
        vm.Y = bounds.Y;
        vm.Width = bounds.Width;
        vm.Height = bounds.Height;
        MarkModified();
    }

    public void SelectOnly(NodeViewModelBase vm)
    {
        ClearConnectorSelection();
        foreach (NodeViewModelBase n in Nodes)
        {
            n.IsSelected = ReferenceEquals(n, vm);
        }

        RaiseSelectionChanged();
    }

    public void ToggleSelect(NodeViewModelBase vm)
    {
        ClearConnectorSelection();
        vm.IsSelected = !vm.IsSelected;
        RaiseSelectionChanged();
    }

    public void ClearSelection()
    {
        ClearConnectorSelection();
        foreach (NodeViewModelBase n in Nodes)
        {
            n.IsSelected = false;
        }

        RaiseSelectionChanged();
    }

    public void SelectInRect(Rect2D rect, bool additive)
    {
        ClearConnectorSelection();
        foreach (NodeViewModelBase n in Nodes)
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

        RaiseSelectionChanged();
    }

    public void SelectConnector(ConnectorViewModel connector)
    {
        foreach (NodeViewModelBase n in Nodes)
        {
            n.IsSelected = false;
        }

        foreach (ConnectorViewModel c in Connectors)
        {
            c.IsSelected = ReferenceEquals(c, connector);
        }

        SelectedConnector = connector;
        RaiseSelectionChanged();
    }

    public ConnectorViewModel? HitTestConnector(Point2D world, double tolerance)
    {
        ConnectorViewModel? best = null;
        double bestDistance = tolerance;
        foreach (ConnectorViewModel c in Connectors)
        {
            double distance = ConnectorDistance(c, world);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = c;
            }
        }

        return best;
    }

    public void Undo()
    {
        _document = _undo.Undo(_document);
        RebuildNodes();
        RebuildConnectors();
        RecomputeModified();
        RaiseUndoState();
    }

    public void Redo()
    {
        _document = _undo.Redo(_document);
        RebuildNodes();
        RebuildConnectors();
        RecomputeModified();
        RaiseUndoState();
    }

    public void MarkModified() => IsModified = true;

    public void MarkSaved(string path)
    {
        FilePath = path;
        IsModified = false;
        _cleanSnapshot = _serializer.Serialize(_document);
    }

    // After undo/redo the document is dirty only if it differs from the last saved/loaded state.
    private void RecomputeModified()
        => IsModified = !string.Equals(_serializer.Serialize(_document), _cleanSnapshot, StringComparison.Ordinal);

    public void NotifyStyleEditStarting() => CaptureUndo();

    void INodeEditContext.BeginMemberEdit() => CaptureUndo();

    void INodeEditContext.EndMemberEdit() => MarkModified();

    public IReadOnlyList<string> GetTypeSuggestions()
    {
        IEnumerable<string> names = _document.Nodes
            .OfType<ClassNode>()
            .Select(c => c.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n));

        return PrimitiveTypes.All
            .Concat(names)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public NodeViewModelBase? FindNode(Guid id) => Nodes.FirstOrDefault(n => n.Id == id);

    private void ClearConnectorSelection()
    {
        foreach (ConnectorViewModel c in Connectors)
        {
            c.IsSelected = false;
        }

        SelectedConnector = null;
    }

    private int NextZIndex() => _document.Nodes.Count == 0 ? 0 : _document.Nodes.Max(n => n.ZIndex) + 1;

    private void RebuildNodes()
    {
        Nodes.Clear();
        foreach (NodeBase node in _document.Nodes.OrderBy(n => n.ZIndex))
        {
            Nodes.Add(CreateNodeViewModel(node));
        }

        RaiseSelectionChanged();
    }

    private NodeViewModelBase CreateNodeViewModel(NodeBase node) => node switch
    {
        ClassNode @class => new ClassNodeViewModel(@class, this, _theme),
        ActorNode actor => new ActorNodeViewModel(actor, _theme),
        UseCaseNode useCase => new UseCaseNodeViewModel(useCase, _theme),
        SystemBoundaryNode boundary => new SystemBoundaryNodeViewModel(boundary, _theme),
        ShapeNode shape => new ShapeNodeViewModel(shape, _theme),
        _ => throw new NotSupportedException($"Unsupported node type: {node.GetType().Name}"),
    };

    // On theme change, re-raise style-derived brushes so default-styled nodes adopt the new theme's
    // fill/text colours (user-customised colours are unaffected — see NodeViewModelBase.UsesDefault*).
    private void OnThemeChanged(object? sender, EventArgs e)
    {
        foreach (NodeViewModelBase node in Nodes)
        {
            node.RaiseStyleChanged();
        }
    }

    /// <summary>Detaches from the shared theme service when the tab closes, so this VM can be collected.</summary>
    public void Dispose() => _theme.ThemeChanged -= OnThemeChanged;

    private void RebuildConnectors()
    {
        foreach (ConnectorViewModel existing in Connectors)
        {
            existing.Detach();
        }

        Connectors.Clear();
        SelectedConnector = null;

        // Non-destructive: build view models only for connectors whose endpoints resolve to
        // current node view models. Connectors are NOT removed from the model here — genuine
        // orphan removal happens explicitly in DeleteSelected.
        Dictionary<Guid, NodeViewModelBase> byId = Nodes.ToDictionary(n => n.Id);
        foreach (Connector connector in _document.Connectors)
        {
            if (byId.TryGetValue(connector.SourceNodeId, out NodeViewModelBase? source)
                && byId.TryGetValue(connector.TargetNodeId, out NodeViewModelBase? target))
            {
                Connectors.Add(new ConnectorViewModel(connector, source, target, _router));
            }
        }
    }

    private static double ConnectorDistance(ConnectorViewModel connector, Point2D world)
    {
        IReadOnlyList<Point2D> points = connector.GetFlattenedPoints();
        double min = double.PositiveInfinity;
        for (int i = 1; i < points.Count; i++)
        {
            min = Math.Min(min, DistanceToSegment(world, points[i - 1], points[i]));
        }

        return min;
    }

    private static double DistanceToSegment(Point2D p, Point2D a, Point2D b)
    {
        Point2D ab = b - a;
        double lengthSquared = (ab.X * ab.X) + (ab.Y * ab.Y);
        if (lengthSquared <= double.Epsilon)
        {
            return p.DistanceTo(a);
        }

        double t = (((p.X - a.X) * ab.X) + ((p.Y - a.Y) * ab.Y)) / lengthSquared;
        t = Math.Clamp(t, 0d, 1d);
        Point2D projection = new(a.X + (t * ab.X), a.Y + (t * ab.Y));
        return p.DistanceTo(projection);
    }

    private void RaiseUndoState()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        UndoStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseSelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanAlignSelection));
        OnPropertyChanged(nameof(CanDistributeSelection));
        OnPropertyChanged(nameof(CanSpaceConnections));
        AlignCommand.NotifyCanExecuteChanged();
        DistributeCommand.NotifyCanExecuteChanged();
        SpaceConnectionsCommand.NotifyCanExecuteChanged();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
