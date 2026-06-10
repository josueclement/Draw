using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Draw.App.Configuration;
using Draw.App.Services;
using Draw.Diagramming.Geometry;
using Draw.Diagramming.Layout;
using Draw.Diagramming.Routing;
using Draw.Diagramming.Styling;
using Draw.Diagramming.Uml;
using Draw.Diagramming.Undo;
using Draw.Model.Connectors;
using Draw.Model.Documents;
using Draw.Model.Nodes;
using Draw.Model.Primitives;
using Draw.Model.Serialization;
using Draw.Model.Styling;

namespace Draw.App.ViewModels;

/// <summary>Editor state and mutating operations for one open diagram (one tab).</summary>
public sealed class DiagramDocumentViewModel : ViewModelBase, INodeEditContext, IDocumentEditContext, IDisposable
{
    private readonly IUndoService _undo;
    private readonly IConnectorRouter _router;
    private readonly IDocumentSerializer _serializer;
    private readonly EditorOptions _options;
    private readonly IThemeService _theme;
    private readonly ClipboardCoordinator _clipboardCoordinator;
    private DiagramDocument _document;
    private string _cleanSnapshot;

    // Transient (never serialized), per-tab "alignment reference": the node ids that stay put while the
    // current selection (the "movers") is lined up against their combined bounding box. Stale ids are
    // pruned in RaiseSelectionChanged, so the live reference always reflects existing shapes.
    private readonly HashSet<Guid> _referenceIds = new();

    // Ribbon View-tab zoom step + bounds; bounds mirror the Ctrl+wheel clamp in DiagramView.
    private const double ZoomStep = 1.2d;
    private const double MinZoom = 0.1d;
    private const double MaxZoom = 8d;

    // Padding (world units) kept around content when fitting it to the viewport.
    private const double FitMargin = 40d;

    public DiagramDocumentViewModel(
        DiagramDocument document,
        IUndoService undo,
        IConnectorRouter router,
        IDocumentSerializer serializer,
        EditorOptions options,
        IThemeService theme,
        IClipboardService clipboard,
        string? filePath)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _undo = undo ?? throw new ArgumentNullException(nameof(undo));
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        _clipboardCoordinator = new ClipboardCoordinator(this, _serializer, clipboard);
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
        MergeConnectionsCommand = new RelayCommand(MergeSelectedConnections, () => CanMergeConnections);
        OrderCommand = new RelayCommand<ZOrderOperation>(
            op => { if (op is { } o) ReorderSelected(o); },
            _ => HasNodeSelection);
        // Also selection-gated (CanExecute depends on node count) and notified in
        // RaiseSelectionChanged, so it likewise must exist before RebuildNodes().
        FitToContentCommand = new RelayCommand(FitToContent, () => Nodes.Count > 0);
        // Relative-alignment commands; selection-gated and notified in RaiseSelectionChanged, so they
        // must exist before RebuildNodes() too.
        SetReferenceCommand = new RelayCommand(SetReference, () => CanSetReference);
        ClearReferenceCommand = new RelayCommand(ClearReference, () => HasReference);
        AlignToReferenceCommand = new RelayCommand<AlignmentMode>(
            mode => { if (mode is { } m) AlignSelectedToReference(m); },
            _ => CanAlignToReference);

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

    public RelayCommand FitToContentCommand { get; }

    public RelayCommand<AlignmentMode> AlignCommand { get; }

    public RelayCommand<DistributionMode> DistributeCommand { get; }

    public RelayCommand SpaceConnectionsCommand { get; }

    public RelayCommand MergeConnectionsCommand { get; }

    public RelayCommand<ZOrderOperation> OrderCommand { get; }

    /// <summary>Captures the current selection as the fixed alignment reference.</summary>
    public RelayCommand SetReferenceCommand { get; }

    /// <summary>Clears the captured alignment reference.</summary>
    public RelayCommand ClearReferenceCommand { get; }

    /// <summary>Lines the movers (selection minus reference) up against the reference's bounding box.</summary>
    public RelayCommand<AlignmentMode> AlignToReferenceCommand { get; }

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

    /// <summary>Visible viewport size in screen pixels, pushed by the view; used to centre pastes.</summary>
    public double ViewportWidth
    {
        get;
        set => SetProperty(ref field, value);
    }

    public double ViewportHeight
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool CanUndo => _undo.CanUndo;

    public bool CanRedo => _undo.CanRedo;

    public bool HasSelection => Nodes.Any(n => n.IsSelected) || SelectedConnector is not null;

    /// <summary>True when at least one node is selected — gates Copy/Cut/Duplicate.</summary>
    public bool HasNodeSelection => Nodes.Any(n => n.IsSelected);

    /// <summary>Alignment needs at least two shapes to have a common edge/center to line up on.</summary>
    public bool CanAlignSelection => SelectedNodes.Count() >= 2;

    /// <summary>Distribution needs at least three shapes (two anchors plus something to space between).</summary>
    public bool CanDistributeSelection => SelectedNodes.Count() >= 3;

    /// <summary>Connector spacing operates on every connector attached to the selected shape(s).</summary>
    public bool CanSpaceConnections => SelectedNodes.Any();

    /// <summary>Connector merging operates on every connector attached to the selected shape(s).</summary>
    public bool CanMergeConnections => SelectedNodes.Any();

    /// <summary>The captured reference nodes that still exist in the document (stale ids are ignored).</summary>
    public IEnumerable<NodeViewModelBase> ReferenceNodes => Nodes.Where(n => _referenceIds.Contains(n.Id));

    /// <summary>True while an alignment reference is captured (at least one reference node still present).</summary>
    public bool HasReference => ReferenceNodes.Any();

    /// <summary>The selected nodes that will actually move — the selection minus any reference nodes.</summary>
    private IEnumerable<NodeViewModelBase> MoverNodes => SelectedNodes.Where(n => !_referenceIds.Contains(n.Id));

    /// <summary>"Set as reference" needs at least one selected node to capture.</summary>
    public bool CanSetReference => HasNodeSelection;

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

    private const double EntityNodeDefaultWidth = 180d;
    private const double EntityNodeDefaultHeight = 120d;

    public EntityNodeViewModel AddEntityNode(Point2D center)
    {
        CaptureUndo();

        double w = EntityNodeDefaultWidth;
        double h = EntityNodeDefaultHeight;
        Rect2D bounds = new(center.X - (w / 2), center.Y - (h / 2), w, h);
        if (SnapEnabled)
        {
            bounds = bounds.PositionSnappedToGrid(GridSize);
        }

        EntityNode node = new()
        {
            Name = "Table",
            Bounds = bounds,
            Style = _document.DefaultShapeStyle.Clone(),
            ZIndex = NextZIndex(),
        };

        _document.Nodes.Add(node);
        EntityNodeViewModel vm = new(node, this, _theme);
        Nodes.Add(vm);
        SelectOnly(vm);
        MarkModified();
        return vm;
    }

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
            Route = RouteStyle.Rounded,
        };
        _document.Connectors.Add(connector);
        ConnectorViewModel vm = new(connector, source, target, _router, _theme);
        Connectors.Add(vm);

        // Curve-on-connect: pin each end to the centre of the side it naturally attaches to. An
        // unpinned rounded connector aims dead-on at the other shape and renders straight; a
        // side-centre anchor gives the cardinal outward normal that makes the rounded route bow
        // into a curve immediately. Classify both sides from the initial auto-route before pinning,
        // since pinning the source recomputes the route (and would move the target attachment).
        Point2D autoStart = vm.RouteStart;
        Point2D autoEnd = vm.RouteEnd;
        BoxSide sourceSide = ConnectionDistributor.ClassifySide(source.Bounds, autoStart);
        BoxSide targetSide = ConnectionDistributor.ClassifySide(target.Bounds, autoEnd);
        vm.SetSourceAnchor(ConnectionDistributor.EvenAnchor(sourceSide, 0, 1));
        vm.SetTargetAnchor(ConnectionDistributor.EvenAnchor(targetSide, 0, 1));

        SelectConnector(vm);
        MarkModified();
        return vm;
    }

    /// <summary>Copies the selection to the clipboard. See <see cref="ClipboardCoordinator"/>.</summary>
    public Task CopySelectionAsync() => _clipboardCoordinator.CopySelectionAsync();

    /// <summary>Copies the selection, then deletes it. See <see cref="ClipboardCoordinator"/>.</summary>
    public Task CutSelectionAsync() => _clipboardCoordinator.CutSelectionAsync();

    /// <summary>Pastes the clipboard payload (or an external bitmap). See <see cref="ClipboardCoordinator"/>.</summary>
    public Task PasteAsync() => _clipboardCoordinator.PasteAsync();

    /// <summary>Clones the selection in place with a small offset. See <see cref="ClipboardCoordinator"/>.</summary>
    public void DuplicateSelection() => _clipboardCoordinator.DuplicateSelection();

    /// <summary>Inserts an image node centred on <paramref name="centre"/> (one undo step). Used by paste,
    /// file insert and drag-drop. See <see cref="ClipboardCoordinator"/>.</summary>
    public ImageNodeViewModel AddImageNode(Point2D centre, byte[] data, string format)
        => _clipboardCoordinator.AddImageNode(centre, data, format);

    /// <summary>Inserts an image centred on the current viewport (for the file-picker entry point).</summary>
    public ImageNodeViewModel AddImageAtViewportCenter(byte[] data, string format)
        => _clipboardCoordinator.AddImageAtViewportCenter(data, format);

    /// <summary>
    /// World-coordinate bounding box of all diagram content: every node rectangle plus each
    /// connector's routed geometry (so manual bends extending past the nodes are included).
    /// Returns null when the diagram has no nodes. Drives the canvas scrollbars and Fit-to-content.
    /// </summary>
    public Rect2D? GetContentBounds()
    {
        if (Nodes.Count == 0)
        {
            return null;
        }

        Rect2D bounds = Nodes[0].Bounds;
        for (int i = 1; i < Nodes.Count; i++)
        {
            bounds = bounds.Union(Nodes[i].Bounds);
        }

        foreach (ConnectorViewModel connector in Connectors)
        {
            foreach (Point2D point in connector.GetFlattenedPoints())
            {
                bounds = bounds.Union(new Rect2D(point.X, point.Y, 0d, 0d));
            }
        }

        return bounds;
    }

    /// <summary>Zooms/pans so all content fits centred in the viewport, never enlarging past 100%.</summary>
    private void FitToContent()
    {
        if (GetContentBounds() is not { } content || ViewportWidth <= 0 || ViewportHeight <= 0)
        {
            return;
        }

        Rect2D b = content.Inflate(FitMargin);
        double fit = Math.Min(ViewportWidth / b.Width, ViewportHeight / b.Height);
        double zoom = Math.Clamp(Math.Min(fit, 1d), MinZoom, MaxZoom);
        Zoom = zoom;
        PanX = (ViewportWidth / 2d) - (b.Center.X * zoom);
        PanY = (ViewportHeight / 2d) - (b.Center.Y * zoom);
    }

    private Point2D ViewportCenterWorld()
    {
        double zoom = Zoom <= 0 ? 1d : Zoom;
        return new Point2D(((ViewportWidth / 2d) - PanX) / zoom, ((ViewportHeight / 2d) - PanY) / zoom);
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

        List<NodeViewModelBase> nodes = SelectedNodes.ToList();
        if (nodes.Count == 0)
        {
            return;
        }

        // Snap the selection as a single unit: derive one offset from the bounding-box top-left and apply
        // it to every node. A lone shape still lands on the grid; a multi-shape group keeps its relative
        // spacing, so shapes laid out with Align/Distribute don't drift apart when the group is moved.
        double minX = nodes.Min(n => n.Model.Bounds.X);
        double minY = nodes.Min(n => n.Model.Bounds.Y);
        Point2D origin = new(minX, minY);
        Point2D snapped = origin.SnappedToGrid(GridSize);
        double dx = snapped.X - origin.X;
        double dy = snapped.Y - origin.Y;
        if (dx == 0d && dy == 0d)
        {
            return;
        }

        foreach (NodeViewModelBase vm in nodes)
        {
            vm.X += dx;
            vm.Y += dy;
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
        // Selection set is unchanged, but its geometry moved — re-raise so the view rebuilds
        // the overlay resize handles at the new positions (they aren't data-bound).
        RaiseSelectionChanged();
    }

    /// <summary>
    /// Captures the current selection as the alignment reference: those shapes stay put while a later
    /// selection is lined up against them. Clears the live selection so the next pick is the movers; the
    /// reference keeps its own highlight independently. Transient (not serialized) and per-tab.
    /// </summary>
    public void SetReference()
    {
        HashSet<Guid> ids = SelectedNodes.Select(n => n.Id).ToHashSet();
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
        ClearSelection();
    }

    /// <summary>Clears the alignment reference. Transient state only — no document mutation, no undo step.</summary>
    public void ClearReference()
    {
        if (_referenceIds.Count == 0)
        {
            return;
        }

        _referenceIds.Clear();
        RaiseSelectionChanged();
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

        CaptureUndo();
        IReadOnlyList<Rect2D> result = ShapeArranger.AlignToReference(
            movers.Select(n => n.Model.Bounds).ToList(), referenceBox, mode);
        for (int i = 0; i < movers.Count; i++)
        {
            movers[i].X = result[i].X;
            movers[i].Y = result[i].Y;
        }

        MarkModified();
        RaiseSelectionChanged();
    }

    /// <summary>
    /// Changes the front-to-back stacking of the selected shapes (one undo step). Nodes are kept in two
    /// bands — system boundaries always below ordinary shapes — and each band is reordered independently,
    /// so a boundary can be re-stacked relative to other boundaries but can never rise above a shape. The
    /// new order is repacked into contiguous <c>ZIndex</c> values; connectors render in their own layer
    /// above all nodes, so they stay on top regardless. A no-op (no undo) when nothing actually moves.
    /// </summary>
    public void ReorderSelected(ZOrderOperation operation)
    {
        HashSet<NodeViewModelBase> selectedSet = SelectedNodes.ToHashSet();
        if (selectedSet.Count == 0)
        {
            return;
        }

        // Boundaries form a lower band that ordinary shapes can never sink below; the banded reorder
        // (back-to-front by current ZIndex within each band) lives in the testable Diagramming layer.
        IReadOnlyList<NodeViewModelBase> reordered = ZOrderArranger.ReorderInBands(
            Nodes,
            n => n.Model is SystemBoundaryNode,
            selectedSet.Contains,
            n => n.Model.ZIndex,
            operation);

        // The current banded order (boundaries first, each band by ZIndex) equals the reorder when nothing
        // moves — so this detects a no-op (e.g. the selection is already at the front) and avoids dirtying.
        IReadOnlyList<NodeViewModelBase> current = Nodes
            .OrderByDescending(n => n.Model is SystemBoundaryNode)
            .ThenBy(n => n.Model.ZIndex)
            .ToList();
        if (reordered.SequenceEqual(current))
        {
            return;
        }

        CaptureUndo();
        for (int i = 0; i < reordered.Count; i++)
        {
            reordered[i].Model.ZIndex = i;
        }

        // Mirror the new order into the node collection so the ItemsControl restacks: render order
        // follows collection order (and the bound ZIndex agrees with it). In-place Move keeps the
        // existing view-model instances — and thus their selection and decoded image bitmaps — intact.
        for (int target = 0; target < reordered.Count; target++)
        {
            int currentIndex = Nodes.IndexOf(reordered[target]);
            if (currentIndex != target)
            {
                Nodes.Move(currentIndex, target);
            }
        }

        // Refresh the bound ZIndex (Visual.ZIndex) too, so it stays consistent with the new order.
        foreach (NodeViewModelBase node in Nodes)
        {
            node.RaiseZIndexChanged();
        }

        MarkModified();
    }

    /// <summary>
    /// Force-pins every connector end touching the selected shape(s) into evenly spaced positions (one
    /// undo step): on each bounding-box side the ends keep their current order and are re-pinned at equal
    /// gaps, and a side with a single end is centred on that edge.
    /// </summary>
    public void SpaceSelectedConnections() => PinSelectedConnectionEnds(ConnectionDistributor.EvenAnchor);

    /// <summary>
    /// Force-pins every connector end touching the selected shape(s) onto the centre of the side it lands
    /// on (one undo step) — the inverse of <see cref="SpaceSelectedConnections"/>. Every end on a given
    /// side collapses to that edge's midpoint (they stack, by design), so the two actions let the user fan
    /// out and regroup a shape's connectors.
    /// </summary>
    public void MergeSelectedConnections()
        => PinSelectedConnectionEnds((side, _, _) => ConnectionDistributor.EvenAnchor(side, 0, 1));

    /// <summary>
    /// Re-pins every connector end touching the selected shape(s), using <paramref name="anchorFor"/> to
    /// choose the relative (u,v) anchor for the <c>i</c>-th of <c>count</c> ends on a bounding-box side.
    /// Reads the current routes before mutating, so each end is classified by where it lands now; captures
    /// one undo entry on the first real change, and a no-op (nothing actually changes) adds no undo entry.
    /// </summary>
    private void PinSelectedConnectionEnds(Func<BoxSide, int, int, Point2D> anchorFor)
    {
        HashSet<Guid> selectedIds = SelectedNodes.Select(n => n.Id).ToHashSet();
        if (selectedIds.Count == 0)
        {
            return;
        }

        // Snapshot every end touching a selected shape (read the current routes up front — a connector's
        // route depends only on its own endpoints, so this is order-independent). The pure planner groups
        // by side, keeps the current order, and returns only the ends whose anchor actually changes.
        List<ConnectionDistributor.PinningEnd<(ConnectorViewModel Connector, bool IsSource)>> ends = new();
        foreach (ConnectorViewModel connector in Connectors)
        {
            if (selectedIds.Contains(connector.Source.Id))
            {
                ends.Add(new ConnectionDistributor.PinningEnd<(ConnectorViewModel Connector, bool IsSource)>(
                    (connector, true), connector.Source.Id, connector.Source.Bounds, connector.RouteStart, connector.SourceAnchor));
            }

            if (selectedIds.Contains(connector.Target.Id))
            {
                ends.Add(new ConnectionDistributor.PinningEnd<(ConnectorViewModel Connector, bool IsSource)>(
                    (connector, false), connector.Target.Id, connector.Target.Bounds, connector.RouteEnd, connector.TargetAnchor));
            }
        }

        IReadOnlyList<((ConnectorViewModel Connector, bool IsSource) Token, Point2D Anchor)> plan =
            ConnectionDistributor.PlanPinning(ends, anchorFor);

        // An empty plan means nothing changes — add no undo entry (the no-op contract).
        if (plan.Count == 0)
        {
            return;
        }

        CaptureUndo();
        foreach (((ConnectorViewModel Connector, bool IsSource) Token, Point2D Anchor) op in plan)
        {
            if (op.Token.IsSource)
            {
                op.Token.Connector.SetSourceAnchor(op.Anchor);
            }
            else
            {
                op.Token.Connector.SetTargetAnchor(op.Anchor);
            }
        }

        MarkModified();
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

    /// <summary>Selects exactly the given nodes (clearing any other selection). Used after paste/duplicate.</summary>
    public void SelectNodes(IReadOnlyCollection<NodeViewModelBase> nodes)
    {
        ClearConnectorSelection();
        HashSet<NodeViewModelBase> set = nodes as HashSet<NodeViewModelBase> ?? new HashSet<NodeViewModelBase>(nodes);
        foreach (NodeViewModelBase n in Nodes)
        {
            n.IsSelected = set.Contains(n);
        }

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

    /// <summary>Applies a quick-palette swatch to the whole selection: a coordinated fill + stroke +
    /// text on every selected node, and stroke + label colour on a selected connector. Stores the
    /// swatch id so the colours follow the active theme, and bakes the current theme's variant into the
    /// raw colour fields as a fallback. One undo step; a no-op (and no undo) when nothing is selected.</summary>
    public void ApplyStyleSwatch(StyleSwatch swatch)
    {
        SwatchVariant v = swatch.Variant(_theme.IsDark);
        ApplyStyleToSelection(
            style =>
            {
                style.PaletteId = swatch.Id;
                style.Fill = v.Fill;
                style.Stroke.Color = v.Stroke;
                style.Font.Color = v.Text;
            },
            style =>
            {
                style.PaletteId = swatch.Id;
                style.Stroke.Color = v.Stroke;
                style.Font.Color = v.Text;
            });
    }

    /// <summary>Restores the theme-adaptive default look (default fill/stroke/text) and unlinks the
    /// selection from any palette swatch. Thickness, dash and font are left untouched.</summary>
    public void ResetStyleToDefault()
        => ApplyStyleToSelection(
            style =>
            {
                style.PaletteId = null;
                style.Fill = ShapeStyle.DefaultFill;
                style.Stroke.Color = StrokeStyle.DefaultColor;
                style.Font.Color = FontSpec.DefaultColor;
            },
            style =>
            {
                style.PaletteId = null;
                style.Stroke.Color = StrokeStyle.DefaultColor;
                style.Font.Color = FontSpec.DefaultColor;
            });

    /// <summary>Makes the selected nodes outline-only (transparent fill) and unlinks them from any
    /// palette swatch. Connectors have no fill, so they're unaffected.</summary>
    public void ApplyNoFill()
        => ApplyStyleToSelection(
            style =>
            {
                style.PaletteId = null;
                style.Fill = ArgbColor.Transparent;
            },
            mutateConnector: null);

    // Shared body for the palette actions: one undo snapshot, mutate every selected node + a selected
    // connector, refresh bindings, mark dirty. No-op (no undo) when the selection is empty.
    private void ApplyStyleToSelection(Action<ShapeStyle> mutateNode, Action<ConnectorStyle>? mutateConnector)
    {
        List<NodeViewModelBase> nodes = SelectedNodes.ToList();
        ConnectorViewModel? connector = SelectedConnector;
        if (nodes.Count == 0 && connector is null)
        {
            return;
        }

        NotifyStyleEditStarting();
        foreach (NodeViewModelBase node in nodes)
        {
            mutateNode(node.Model.Style);
            node.RaiseStyleChanged();
        }

        if (connector is not null && mutateConnector is not null)
        {
            mutateConnector(connector.Model.Style);
            connector.RaiseStyleChanged();
        }

        MarkModified();
    }

    void INodeEditContext.BeginMemberEdit() => CaptureUndo();

    void INodeEditContext.EndMemberEdit() => MarkModified();

    // IDocumentEditContext seams that aren't already public — exposed to the composed coordinators
    // (e.g. ClipboardCoordinator) without widening the view model's public surface.
    NodeViewModelBase IDocumentEditContext.CreateNodeViewModel(NodeBase node) => CreateNodeViewModel(node);

    void IDocumentEditContext.RebuildConnectors() => RebuildConnectors();

    void IDocumentEditContext.RaiseSelectionChanged() => RaiseSelectionChanged();

    Point2D IDocumentEditContext.ViewportCenterWorld() => ViewportCenterWorld();

    public IReadOnlyList<string> GetTypeSuggestions()
    {
        IEnumerable<string> classNames = _document.Nodes
            .OfType<ClassNode>()
            .Select(c => c.Name);
        IEnumerable<string> entityNames = _document.Nodes
            .OfType<EntityNode>()
            .Select(e => e.Name);

        return PrimitiveTypes.All
            .Concat(classNames)
            .Concat(entityNames)
            .Where(n => !string.IsNullOrWhiteSpace(n))
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
        // Dispose any resource-holding node view models (e.g. decoded image bitmaps) before discarding them.
        foreach (NodeViewModelBase existing in Nodes)
        {
            (existing as IDisposable)?.Dispose();
        }

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
        EntityNode entity => new EntityNodeViewModel(entity, this, _theme),
        ActorNode actor => new ActorNodeViewModel(actor, _theme),
        UseCaseNode useCase => new UseCaseNodeViewModel(useCase, _theme),
        SystemBoundaryNode boundary => new SystemBoundaryNodeViewModel(boundary, _theme),
        ImageNode image => new ImageNodeViewModel(image, _theme),
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

        // Palette-linked connectors resolve their stroke/label colour from the active theme too.
        foreach (ConnectorViewModel connector in Connectors)
        {
            connector.RaiseStyleChanged();
        }
    }

    /// <summary>Detaches from the shared theme service when the tab closes, so this VM can be collected,
    /// and disposes any resource-holding node view models (e.g. decoded image bitmaps).</summary>
    public void Dispose()
    {
        _theme.ThemeChanged -= OnThemeChanged;
        foreach (NodeViewModelBase node in Nodes)
        {
            (node as IDisposable)?.Dispose();
        }
    }

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
                Connectors.Add(new ConnectorViewModel(connector, source, target, _router, _theme));
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
        // Drop any reference ids whose node no longer exists (deleted, or gone after undo/redo) so the
        // reference reflects only live shapes — and so a deleted reference can't resurrect on undo.
        if (_referenceIds.Count > 0)
        {
            HashSet<Guid> live = Nodes.Select(n => n.Id).ToHashSet();
            _referenceIds.RemoveWhere(id => !live.Contains(id));
        }

        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasNodeSelection));
        OnPropertyChanged(nameof(CanAlignSelection));
        OnPropertyChanged(nameof(CanDistributeSelection));
        OnPropertyChanged(nameof(CanSpaceConnections));
        OnPropertyChanged(nameof(CanMergeConnections));
        OnPropertyChanged(nameof(HasReference));
        OnPropertyChanged(nameof(CanSetReference));
        OnPropertyChanged(nameof(CanAlignToReference));
        OnPropertyChanged(nameof(ReferenceStatusText));
        AlignCommand.NotifyCanExecuteChanged();
        DistributeCommand.NotifyCanExecuteChanged();
        SpaceConnectionsCommand.NotifyCanExecuteChanged();
        MergeConnectionsCommand.NotifyCanExecuteChanged();
        OrderCommand.NotifyCanExecuteChanged();
        FitToContentCommand.NotifyCanExecuteChanged();
        SetReferenceCommand.NotifyCanExecuteChanged();
        ClearReferenceCommand.NotifyCanExecuteChanged();
        AlignToReferenceCommand.NotifyCanExecuteChanged();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
