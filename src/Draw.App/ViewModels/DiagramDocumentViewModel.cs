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
using Draw.Diagramming.MindMap;
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
public sealed class DiagramDocumentViewModel : ViewModelBase, INodeEditContext, IDocumentEditContext, IViewportHost, IDisposable
{
    private readonly IUndoService _undo;
    private readonly IConnectorRouter _router;
    private readonly IDocumentSerializer _serializer;
    private readonly EditorOptions _options;
    private readonly IThemeService _theme;
    private readonly NodeKindRegistry _nodeKinds;
    private readonly ClipboardCoordinator _clipboardCoordinator;
    private readonly ConnectorSpacingCoordinator _connectorSpacingCoordinator;
    private readonly ZOrderCoordinator _zOrderCoordinator;
    private readonly AlignmentCoordinator _alignmentCoordinator;
    private readonly SelectionCoordinator _selectionCoordinator;
    private readonly ViewportCoordinator _viewportCoordinator;
    private readonly StyleCoordinator _styleCoordinator;
    private DiagramDocument _document;
    private string _cleanSnapshot;

    public DiagramDocumentViewModel(
        DiagramDocument document,
        IUndoService undo,
        IConnectorRouter router,
        IDocumentSerializer serializer,
        EditorOptions options,
        IThemeService theme,
        IClipboardService clipboard,
        NodeKindRegistry nodeKinds,
        string? filePath)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _undo = undo ?? throw new ArgumentNullException(nameof(undo));
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        _nodeKinds = nodeKinds ?? throw new ArgumentNullException(nameof(nodeKinds));
        _clipboardCoordinator = new ClipboardCoordinator(this, _serializer, clipboard);
        _connectorSpacingCoordinator = new ConnectorSpacingCoordinator(this);
        _zOrderCoordinator = new ZOrderCoordinator(this);
        _alignmentCoordinator = new AlignmentCoordinator(this);
        _selectionCoordinator = new SelectionCoordinator(this);
        _viewportCoordinator = new ViewportCoordinator(this);
        _styleCoordinator = new StyleCoordinator(this, _theme);
        FilePath = filePath;
        _undo.StateChanged += (_, _) => RaiseUndoState();
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
        FitToContentCommand = new RelayCommand(_viewportCoordinator.FitToContent, () => Nodes.Count > 0);
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

        ZoomInCommand = new RelayCommand(_viewportCoordinator.ZoomIn);
        ZoomOutCommand = new RelayCommand(_viewportCoordinator.ZoomOut);
        ZoomResetCommand = new RelayCommand(_viewportCoordinator.ZoomReset);
        ToggleGridCommand = new RelayCommand(() => ShowGrid = !ShowGrid);
        SelectAllCommand = new RelayCommand(SelectAll);
    }

    public ObservableCollection<NodeViewModelBase> Nodes { get; } = new();

    public ObservableCollection<ConnectorViewModel> Connectors { get; } = new();

    public DiagramDocument Document => _document;

    public DiagramType DiagramType => _document.DiagramType;

    public double GridSize => _options.GridSize;

    public bool SnapEnabled => _options.SnapToGrid;

    /// <summary>Whether the canvas grid is drawn. Per-document and persisted in the model, so toggling it
    /// marks the document modified; it is a display preference, so it is not captured for undo.</summary>
    public bool ShowGrid
    {
        get => _document.ShowGrid;
        set
        {
            if (_document.ShowGrid != value)
            {
                _document.ShowGrid = value;
                OnPropertyChanged();
                MarkModified();
            }
        }
    }

    /// <summary>Minimum zoom factor, from <see cref="EditorOptions"/> — the single source shared with
    /// the Ctrl+wheel clamp in <c>DiagramView</c>.</summary>
    public double MinZoom => _options.MinZoom;

    /// <summary>Maximum zoom factor; see <see cref="MinZoom"/>.</summary>
    public double MaxZoom => _options.MaxZoom;

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

    /// <summary>Toggles the canvas grid's visibility for this document (the <c>t g</c> chord).</summary>
    public RelayCommand ToggleGridCommand { get; }

    /// <summary>Selects every node and connector in the active document (Ctrl+A).</summary>
    public RelayCommand SelectAllCommand { get; }

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

    public bool HasSelection => Nodes.Any(n => n.IsSelected) || Connectors.Any(c => c.IsSelected);

    /// <summary>True when at least one node is selected — gates Copy/Cut/Duplicate.</summary>
    public bool HasNodeSelection => Nodes.Any(n => n.IsSelected);

    /// <summary>Alignment needs at least two shapes to line up. See <see cref="AlignmentCoordinator"/>.</summary>
    public bool CanAlignSelection => _alignmentCoordinator.CanAlignSelection;

    /// <summary>Distribution needs at least three shapes. See <see cref="AlignmentCoordinator"/>.</summary>
    public bool CanDistributeSelection => _alignmentCoordinator.CanDistributeSelection;

    /// <summary>Connector spacing operates on every connector attached to the selected shape(s).</summary>
    public bool CanSpaceConnections => SelectedNodes.Any();

    /// <summary>Connector merging operates on every connector attached to the selected shape(s).</summary>
    public bool CanMergeConnections => SelectedNodes.Any();

    /// <summary>The captured reference nodes that still exist. See <see cref="AlignmentCoordinator"/>.</summary>
    public IEnumerable<NodeViewModelBase> ReferenceNodes => _alignmentCoordinator.ReferenceNodes;

    /// <summary>True while an alignment reference is captured. See <see cref="AlignmentCoordinator"/>.</summary>
    public bool HasReference => _alignmentCoordinator.HasReference;

    /// <summary>"Set as reference" needs at least one selected node to capture.</summary>
    public bool CanSetReference => _alignmentCoordinator.CanSetReference;

    /// <summary>"Align to reference" needs a captured reference and at least one mover.</summary>
    public bool CanAlignToReference => _alignmentCoordinator.CanAlignToReference;

    /// <summary>Banner text shown while a reference is active. See <see cref="AlignmentCoordinator"/>.</summary>
    public string ReferenceStatusText => _alignmentCoordinator.ReferenceStatusText;

    public bool HasConnectorSelection => Connectors.Any(c => c.IsSelected);

    /// <summary>
    /// The single connector to edit and inspect on its own — non-null <b>only</b> when exactly one
    /// connector is selected and no node is. Multi-connector and mixed shape+connector selections return
    /// null, so the single-connector machinery (endpoint/waypoint/label handles, snapping, the
    /// per-connector inspector fields) engages only when a lone connector is the focus. Computed from the
    /// per-connector <see cref="ConnectorViewModel.IsSelected"/> flags; change notifications are raised
    /// from <see cref="RaiseSelectionChanged"/>.
    /// </summary>
    public ConnectorViewModel? SelectedConnector
    {
        get
        {
            if (Nodes.Any(n => n.IsSelected))
            {
                return null;
            }

            ConnectorViewModel? only = null;
            foreach (ConnectorViewModel c in Connectors)
            {
                if (!c.IsSelected)
                {
                    continue;
                }

                if (only is not null)
                {
                    return null;
                }

                only = c;
            }

            return only;
        }
    }

    /// <summary>Every currently-selected connector. Bulk styling and delete operate on this set.</summary>
    public IEnumerable<ConnectorViewModel> SelectedConnectors => Connectors.Where(c => c.IsSelected);

    public IEnumerable<NodeViewModelBase> SelectedNodes => Nodes.Where(n => n.IsSelected);

    public event EventHandler? UndoStateChanged;

    public event EventHandler? SelectionChanged;

    /// <summary>
    /// Captures a whole-document undo snapshot. Undo capture is split deliberately by altitude, and
    /// callers must know which side they are on:
    /// <list type="bullet">
    /// <item><b>Command-level</b> mutations on this view model (<see cref="AddShape"/>, deletes, style
    /// and z-order changes, …) call this themselves, once, at the start of the operation.</item>
    /// <item><b>Gesture-level</b> continuous mutations (<see cref="MoveSelectedBy"/>,
    /// <see cref="SnapSelectionToGrid"/>) do <b>not</b> self-capture — the view captures once per
    /// gesture (before the per-pixel stream) so a drag yields a single undo entry, not one per pixel.</item>
    /// </list>
    /// </summary>
    public void CaptureUndo() => _undo.Capture(_document);

    public ShapeNodeViewModel AddShape(ShapeKind kind, Point2D center)
    {
        // Shapes keep their configurable default size (EditorOptions); other kinds size from the registry.
        ShapeNode node = new() { Kind = kind, Style = _document.DefaultShapeStyle.Clone() };
        return PlaceNode<ShapeNodeViewModel>(node, center, _options.DefaultShapeWidth, _options.DefaultShapeHeight);
    }

    public ClassNodeViewModel AddClassNode(ClassNodeKind kind, Point2D center)
    {
        ClassNode node = new() { Kind = kind, Name = DefaultClassName(kind), Style = _document.DefaultShapeStyle.Clone() };
        NodeKindDescriptor descriptor = _nodeKinds.For(node);
        return PlaceNode<ClassNodeViewModel>(node, center, descriptor.DefaultWidth, descriptor.DefaultHeight);
    }

    private static string DefaultClassName(ClassNodeKind kind) => kind switch
    {
        ClassNodeKind.Interface => "Interface",
        ClassNodeKind.Enum => "Enumeration",
        _ => "Class",
    };

    public EntityNodeViewModel AddEntityNode(Point2D center)
    {
        EntityNode node = new() { Name = "Table", Style = _document.DefaultShapeStyle.Clone() };
        NodeKindDescriptor descriptor = _nodeKinds.For(node);
        return PlaceNode<EntityNodeViewModel>(node, center, descriptor.DefaultWidth, descriptor.DefaultHeight);
    }

    public NodeViewModelBase AddUseCaseNode(UseCaseNodeKind kind, Point2D center)
    {
        NodeBase node = kind switch
        {
            UseCaseNodeKind.Actor => new ActorNode { Name = "Actor", Style = _document.DefaultShapeStyle.Clone() },
            UseCaseNodeKind.SystemBoundary => new SystemBoundaryNode { Title = "System", Style = _document.DefaultShapeStyle.Clone() },
            _ => new UseCaseNode { Text = "Use case", Style = _document.DefaultShapeStyle.Clone() },
        };
        NodeKindDescriptor descriptor = _nodeKinds.For(node);
        return PlaceNode<NodeViewModelBase>(node, center, descriptor.DefaultWidth, descriptor.DefaultHeight);
    }

    public NodeViewModelBase AddUmlNode(UmlNodeKind kind, Point2D center)
    {
        NodeBase node = kind switch
        {
            UmlNodeKind.Package => new PackageNode { Title = "Package", Style = _document.DefaultShapeStyle.Clone() },
            UmlNodeKind.Component => new ComponentNode { Name = "Component", Style = _document.DefaultShapeStyle.Clone() },
            _ => new DeploymentNode { Name = "Node", Style = _document.DefaultShapeStyle.Clone() },
        };
        NodeKindDescriptor descriptor = _nodeKinds.For(node);
        return PlaceNode<NodeViewModelBase>(node, center, descriptor.DefaultWidth, descriptor.DefaultHeight);
    }

    // The shared create flow for every AddXxx: snap+place the bounds, assign the z-index for the kind's
    // stacking band, add the model + its view model, select it, mark dirty — one undo step. The caller
    // supplies a model node with its kind-specific fields (and Style) already set, plus the placement
    // centre and size. The z-band — and the background kind's draw-behind insertion — is data-driven
    // from the node-kind descriptor, so a system boundary is no longer special-cased by type here.
    private TVm PlaceNode<TVm>(NodeBase node, Point2D center, double w, double h)
        where TVm : NodeViewModelBase
    {
        CaptureUndo();
        return PlaceNodeCore<TVm>(node, center, w, h);
    }

    // The CaptureUndo-free placement core, so a multi-step gesture (e.g. a mind-map child plus its
    // branch connector) can be captured as a single undo step. See the split-capture contract on
    // CaptureUndo — the caller owns the snapshot.
    private TVm PlaceNodeCore<TVm>(NodeBase node, Point2D center, double w, double h)
        where TVm : NodeViewModelBase
    {
        Rect2D bounds = new(center.X - (w / 2), center.Y - (h / 2), w, h);
        if (SnapEnabled)
        {
            bounds = bounds.PositionSnappedToGrid(GridSize);
        }

        node.Bounds = bounds;
        bool background = _nodeKinds.For(node).ZBand == NodeZBand.Background;
        node.ZIndex = background ? NextBackgroundZIndex() : NextZIndex();

        _document.Nodes.Add(node);
        TVm vm = (TVm)CreateNodeViewModel(node);

        // A background node (system boundary) renders behind everything: lowest z-index AND inserted at
        // the front of the (insertion-ordered) collection so it draws first even before a rebuild.
        if (background)
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
        ConnectorViewModel vm = LinkNodesCore(source, target, kind, sourceAnchor: null, targetAnchor: null);

        // A hand-drawn mind-map branch needs its depth (and thus taper) computed from the tree it just
        // joined; ordinary connectors don't, and this is a cheap no-op when there are no branches.
        if (kind == RelationshipKind.MindMapBranch)
        {
            RefreshMindMapBranches();
        }

        SelectConnector(vm);
        MarkModified();
        return vm;
    }

    // The CaptureUndo-free connector-creation core. Adds the connector to the model + view models and
    // pins its endpoints; explicit anchors override the auto side-centre pinning. The caller owns the
    // undo snapshot and selection. Returns the new connector view model.
    private ConnectorViewModel LinkNodesCore(
        NodeViewModelBase source,
        NodeViewModelBase target,
        RelationshipKind kind,
        Point2D? sourceAnchor,
        Point2D? targetAnchor)
    {
        Connector connector = new()
        {
            SourceNodeId = source.Id,
            TargetNodeId = target.Id,
            Kind = kind,
            Route = RouteStyle.Rounded,
        };
        _document.Connectors.Add(connector);
        ConnectorViewModel vm = new(connector, source, target, _router, _theme);
        Connectors.Add(vm);

        if (sourceAnchor is { } sa && targetAnchor is { } ta)
        {
            // Mind-map branch: pin to the explicit side centres (facing → opposite) the '+' implied.
            vm.SetSourceAnchor(sa);
            vm.SetTargetAnchor(ta);
        }
        else
        {
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
        }

        return vm;
    }

    /// <summary>
    /// Spawns a mind-map child of <paramref name="parent"/> on the given <paramref name="side"/>: a topic
    /// that inherits the parent's shape kind and style, placed a gap out on that side (nudged clear of
    /// existing nodes), joined by a tapered mind-map branch (parent → child). One undo step; the child is
    /// left selected for immediate inline editing. Returns null if <paramref name="parent"/> is not placed.
    /// </summary>
    public ShapeNodeViewModel? CreateChildNode(ShapeNodeViewModel parent, BoxSide side)
    {
        if (parent is null || FindNode(parent.Id) is null)
        {
            return null;
        }

        CaptureUndo();

        ShapeNode childModel = new()
        {
            Kind = parent.Model.Kind,
            Style = parent.Model.Style.Clone(),
        };

        double w = _options.DefaultShapeWidth;
        double h = _options.DefaultShapeHeight;
        Point2D center = ComputeChildCenter(parent.Bounds, side, w, h);
        ShapeNodeViewModel child = PlaceNodeCore<ShapeNodeViewModel>(childModel, center, w, h);

        (Point2D sourceAnchor, Point2D targetAnchor) = BranchAnchors(side);
        LinkNodesCore(parent, child, RelationshipKind.MindMapBranch, sourceAnchor, targetAnchor);

        RefreshMindMapBranches();
        MarkModified();
        return child; // PlaceNodeCore already selected it.
    }

    // The initial child centre: out along the clicked side by the parent half-extent + a gap + the child
    // half-extent, then fanned along that side until the child rect clears every existing node.
    private Point2D ComputeChildCenter(Rect2D parent, BoxSide side, double childW, double childH)
    {
        const double gap = 60d;
        Point2D parentCenter = parent.Center;
        Point2D center = side switch
        {
            BoxSide.Left => new Point2D(parent.X - gap - (childW / 2d), parentCenter.Y),
            BoxSide.Right => new Point2D(parent.Right + gap + (childW / 2d), parentCenter.Y),
            BoxSide.Top => new Point2D(parentCenter.X, parent.Y - gap - (childH / 2d)),
            _ => new Point2D(parentCenter.X, parent.Bottom + gap + (childH / 2d)),
        };

        Point2D step = side is BoxSide.Left or BoxSide.Right
            ? new Point2D(0d, childH + 20d)
            : new Point2D(childW + 20d, 0d);

        Point2D candidate = center;
        for (int attempt = 0; attempt < 200 && OverlapsExistingNode(candidate, childW, childH); attempt++)
        {
            int magnitude = (attempt / 2) + 1;
            double signedScale = (attempt % 2 == 0) ? magnitude : -magnitude;
            candidate = center + (step * signedScale);
        }

        return candidate;
    }

    private bool OverlapsExistingNode(Point2D center, double w, double h)
    {
        Rect2D rect = new(center.X - (w / 2d), center.Y - (h / 2d), w, h);
        foreach (NodeViewModelBase node in Nodes)
        {
            // A small inflate keeps a breathing gap so children don't sit flush against a neighbour.
            if (rect.IntersectsWith(node.Bounds.Inflate(10d)))
            {
                return true;
            }
        }

        return false;
    }

    // Pins a branch from the parent's facing-side centre to the child's opposite-side centre, so the
    // rounded route leaves and enters along the clicked axis.
    private static (Point2D Source, Point2D Target) BranchAnchors(BoxSide side) => side switch
    {
        BoxSide.Left => (new Point2D(0d, 0.5d), new Point2D(1d, 0.5d)),
        BoxSide.Right => (new Point2D(1d, 0.5d), new Point2D(0d, 0.5d)),
        BoxSide.Top => (new Point2D(0.5d, 0d), new Point2D(0.5d, 1d)),
        _ => (new Point2D(0.5d, 1d), new Point2D(0.5d, 0d)),
    };

    // Recomputes each mind-map branch's source depth so its taper widths scale with the tree. Cheap
    // no-op when the document has no branches. Called whenever the connector set is rebuilt and after
    // a child is spawned.
    private void RefreshMindMapBranches()
    {
        bool anyBranch = false;
        foreach (ConnectorViewModel vm in Connectors)
        {
            if (vm.IsMindMapBranch)
            {
                anyBranch = true;
                break;
            }
        }

        if (!anyBranch)
        {
            return;
        }

        IReadOnlyDictionary<Guid, int> depths = MindMapHierarchy.ComputeDepths(_document.Connectors);
        foreach (ConnectorViewModel vm in Connectors)
        {
            if (vm.IsMindMapBranch)
            {
                vm.SetBranchDepth(MindMapHierarchy.DepthOf(depths, vm.Source.Id));
            }
        }
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

    public void DeleteSelected()
    {
        List<NodeViewModelBase> selectedNodes = SelectedNodes.ToList();
        List<ConnectorViewModel> selectedConnectors = SelectedConnectors.ToList();
        if (selectedNodes.Count == 0 && selectedConnectors.Count == 0)
        {
            return;
        }

        CaptureUndo();

        // Prune the model: explicitly-selected connectors first, then any connector orphaned by a removed
        // node. RebuildConnectors then resyncs the view models from the pruned model in one pass (it
        // detaches the old connector VMs), so both selected nodes and selected connectors go in one undo step.
        foreach (ConnectorViewModel c in selectedConnectors)
        {
            _document.Connectors.Remove(c.Model);
        }

        if (selectedNodes.Count > 0)
        {
            HashSet<Guid> removedIds = selectedNodes.Select(n => n.Id).ToHashSet();
            foreach (NodeViewModelBase vm in selectedNodes)
            {
                _document.Nodes.Remove(vm.Model);
                Nodes.Remove(vm);
            }

            _document.Connectors.RemoveAll(c => removedIds.Contains(c.SourceNodeId) || removedIds.Contains(c.TargetNodeId));
        }

        RebuildConnectors();
        MarkModified();
        RaiseSelectionChanged();
    }

    /// <summary>
    /// Translates every selected node by (<paramref name="dx"/>, <paramref name="dy"/>). This is a
    /// gesture-level primitive: it deliberately does <b>not</b> capture undo — the caller (the view)
    /// must call <see cref="CaptureUndo"/> once at the start of the gesture. Capturing here would
    /// flood the undo stack with one entry per moved pixel. See <see cref="CaptureUndo"/> for the
    /// split-capture contract.
    /// </summary>
    public void MoveSelectedBy(double dx, double dy)
    {
        foreach (NodeViewModelBase vm in SelectedNodes)
        {
            vm.X += dx;
            vm.Y += dy;
        }

        MarkModified();
    }

    /// <summary>
    /// Snaps the current selection to the grid as a single unit, preserving relative spacing. Like
    /// <see cref="MoveSelectedBy"/> this is a gesture-level primitive and does <b>not</b> self-capture
    /// undo; the caller owns gesture-level <see cref="CaptureUndo"/>. No-op when snapping is disabled.
    /// </summary>
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

    /// <summary>Aligns the selected shapes against their bounding box. See <see cref="AlignmentCoordinator"/>.</summary>
    public void AlignSelected(AlignmentMode mode) => _alignmentCoordinator.AlignSelected(mode);

    /// <summary>Evens out the gaps between the selected shapes. See <see cref="AlignmentCoordinator"/>.</summary>
    public void DistributeSelected(DistributionMode mode) => _alignmentCoordinator.DistributeSelected(mode);

    /// <summary>Captures the current selection as the alignment reference. See <see cref="AlignmentCoordinator"/>.</summary>
    public void SetReference() => _alignmentCoordinator.SetReference();

    /// <summary>Clears the alignment reference. See <see cref="AlignmentCoordinator"/>.</summary>
    public void ClearReference() => _alignmentCoordinator.ClearReference();

    /// <summary>Lines the movers up against the reference's bounding box. See <see cref="AlignmentCoordinator"/>.</summary>
    public void AlignSelectedToReference(AlignmentMode mode) => _alignmentCoordinator.AlignSelectedToReference(mode);

    /// <summary>Changes the front-to-back stacking of the selected shapes. See <see cref="ZOrderCoordinator"/>.</summary>
    public void ReorderSelected(ZOrderOperation operation) => _zOrderCoordinator.ReorderSelected(operation);

    /// <summary>Force-pins selected shapes' connector ends into even spacing. See <see cref="ConnectorSpacingCoordinator"/>.</summary>
    public void SpaceSelectedConnections() => _connectorSpacingCoordinator.SpaceSelectedConnections();

    /// <summary>Regroups selected shapes' connector ends onto their edge midpoints. See <see cref="ConnectorSpacingCoordinator"/>.</summary>
    public void MergeSelectedConnections() => _connectorSpacingCoordinator.MergeSelectedConnections();

    /// <summary>True when every selected node carries <paramref name="marker"/> (and at least one node is
    /// selected) — drives the checked state of the markers context menu and inspector toggles.</summary>
    public bool SelectionHasMarker(NodeMarker marker)
    {
        List<NodeViewModelBase> nodes = SelectedNodes.ToList();
        return nodes.Count > 0 && nodes.All(n => n.Model.Markers.Contains(marker));
    }

    /// <summary>
    /// Toggles <paramref name="marker"/> across the whole node selection as one undo step: if every
    /// selected node already has it, it is removed from all; otherwise it is added to those missing it.
    /// No-op when no node is selected.
    /// </summary>
    public void ToggleNodeMarker(NodeMarker marker)
    {
        List<NodeViewModelBase> nodes = SelectedNodes.ToList();
        if (nodes.Count == 0)
        {
            return;
        }

        CaptureUndo();
        bool removeFromAll = nodes.All(n => n.Model.Markers.Contains(marker));
        foreach (NodeViewModelBase node in nodes)
        {
            if (removeFromAll)
            {
                node.Model.Markers.Remove(marker);
            }
            else if (!node.Model.Markers.Contains(marker))
            {
                node.Model.Markers.Add(marker);
            }

            node.RaiseMarkersChanged();
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

    // These delegate the IsSelected mutations to the SelectionCoordinator, then fire the one
    // RaiseSelectionChanged fan-out the view binds to (the gesture-level capture/notify split is
    // deliberate — see CaptureUndo). The view model stays the façade; the rules live in the coordinator.
    public void SelectOnly(NodeViewModelBase vm)
    {
        _selectionCoordinator.SelectOnly(vm);
        ActiveNode = vm;
        RaiseSelectionChanged();
    }

    public void ToggleSelect(NodeViewModelBase vm)
    {
        _selectionCoordinator.ToggleSelect(vm);
        RaiseSelectionChanged();
    }

    /// <summary>Selects exactly the given nodes (clearing any other selection). Used after paste/duplicate.</summary>
    public void SelectNodes(IReadOnlyCollection<NodeViewModelBase> nodes)
    {
        _selectionCoordinator.SelectNodes(nodes);
        RaiseSelectionChanged();
    }

    public void ClearSelection()
    {
        _selectionCoordinator.ClearSelection();
        ActiveNode = null;
        RaiseSelectionChanged();
    }

    /// <summary>Selects every node and connector in the document (Ctrl+A).</summary>
    public void SelectAll()
    {
        _selectionCoordinator.SelectAll();
        RaiseSelectionChanged();
    }

    public void SelectInRect(Rect2D rect, bool additive)
    {
        _selectionCoordinator.SelectInRect(rect, additive);
        RaiseSelectionChanged();
    }

    /// <summary>Selects exactly one connector, clearing every other node and connector (plain click).</summary>
    public void SelectConnector(ConnectorViewModel connector)
    {
        _selectionCoordinator.SelectConnector(connector);
        RaiseSelectionChanged();
    }

    /// <summary>Adds or removes one connector from the selection without disturbing other selected
    /// connectors or nodes (Shift+click). Enables mixed shape+connector selections.</summary>
    public void ToggleSelectConnector(ConnectorViewModel connector)
    {
        _selectionCoordinator.ToggleSelectConnector(connector);
        RaiseSelectionChanged();
    }

    /// <summary>Adds or removes one node from the selection while leaving any selected connectors intact
    /// (Shift+click). Unlike <see cref="ToggleSelect"/>, it does not clear the connector selection.</summary>
    public void ToggleSelectUnified(NodeViewModelBase node)
    {
        _selectionCoordinator.ToggleSelectUnified(node);
        RaiseSelectionChanged();
    }

    // --- Vim h/j/k/l keyboard navigation ---------------------------------------------------------

    /// <summary>
    /// The keyboard-navigation cursor for vim h/j/k/l: the node a directional move grows from. Seeded by a
    /// plain click / single selection and advanced by each move. Transient (not part of the document, not
    /// undone); a stale reference (after a rebuild or delete) is ignored because it is no longer in
    /// <see cref="Nodes"/>.
    /// </summary>
    public NodeViewModelBase? ActiveNode { get; private set; }

    /// <summary>
    /// Moves the selection to the nearest node in <paramref name="direction"/> (vim h/j/k/l). With
    /// <paramref name="extend"/> (Ctrl) the target is added to the selection and the cursor advances onto it
    /// (a growing chain); otherwise it becomes the sole selection. With nothing selected, the first move just
    /// selects the node nearest the viewport centre as a starting anchor.
    /// </summary>
    public void SelectNearestInDirection(MoveDirection direction, bool extend)
    {
        if (Nodes.Count == 0)
        {
            return;
        }

        NodeViewModelBase? anchor = ActiveNode is { } current && Nodes.Contains(current) ? current : null;
        if (anchor is null && !SelectedNodes.Any())
        {
            SelectOnly(NearestNodeTo(WorldViewportCenter())); // Seed the cursor; ignore direction this once.
            return;
        }

        Point2D reference = anchor?.Bounds.Center ?? SelectionBoundsCenter();
        List<Rect2D> bounds = new(Nodes.Count);
        foreach (NodeViewModelBase node in Nodes)
        {
            bounds.Add(node.Bounds);
        }

        int? index = DirectionalNavigator.FindNearest(reference, bounds, direction);
        if (index is null)
        {
            return; // Nothing in that direction.
        }

        NodeViewModelBase target = Nodes[index.Value];
        if (extend)
        {
            _selectionCoordinator.Select(target);
            RaiseSelectionChanged();
        }
        else
        {
            _selectionCoordinator.SelectOnly(target);
            RaiseSelectionChanged();
        }

        ActiveNode = target;
    }

    private NodeViewModelBase NearestNodeTo(Point2D point)
    {
        NodeViewModelBase best = Nodes[0];
        double bestDistance = double.MaxValue;
        foreach (NodeViewModelBase node in Nodes)
        {
            double distance = node.Bounds.Center.DistanceTo(point);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = node;
            }
        }

        return best;
    }

    private Point2D SelectionBoundsCenter()
    {
        Rect2D box = Rect2D.Empty;
        bool any = false;
        foreach (NodeViewModelBase node in SelectedNodes)
        {
            box = any ? box.Union(node.Bounds) : node.Bounds;
            any = true;
        }

        return any ? box.Center : WorldViewportCenter();
    }

    private Point2D WorldViewportCenter()
    {
        double zoom = Zoom <= 0 ? 1d : Zoom;
        return new Point2D(((ViewportWidth / 2) - PanX) / zoom, ((ViewportHeight / 2) - PanY) / zoom);
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

    /// <summary>Applies a quick-palette swatch to the whole selection (see <see cref="StyleCoordinator"/>).</summary>
    public void ApplyStyleSwatch(StyleSwatch swatch) => _styleCoordinator.ApplyStyleSwatch(swatch);

    /// <summary>Resets the selection to the default ("Blue") swatch — the style new shapes get.</summary>
    public void ResetStyleToDefault() => _styleCoordinator.ResetStyleToDefault();

    /// <summary>Makes the selected nodes outline-only (transparent fill), unlinked from any swatch.</summary>
    public void ApplyNoFill() => _styleCoordinator.ApplyNoFill();

    void INodeEditContext.BeginMemberEdit() => CaptureUndo();

    void INodeEditContext.EndMemberEdit() => MarkModified();

    // IDocumentEditContext seams that aren't already public — exposed to the composed coordinators
    // (e.g. ClipboardCoordinator) without widening the view model's public surface.
    NodeViewModelBase IDocumentEditContext.CreateNodeViewModel(NodeBase node) => CreateNodeViewModel(node);

    void IDocumentEditContext.RebuildConnectors() => RebuildConnectors();

    void IDocumentEditContext.RaiseSelectionChanged() => RaiseSelectionChanged();

    Point2D IDocumentEditContext.ViewportCenterWorld() => _viewportCoordinator.ViewportCenterWorld();

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

    public int NextZIndex() => _document.Nodes.Count == 0 ? 0 : _document.Nodes.Max(n => n.ZIndex) + 1;

    public int NextBackgroundZIndex() => LowestZIndex() - 1;

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

    // Delegates to the node-kind registry (the single source pairing model type → view-model factory),
    // which replaces the former runtime-throwing type switch; coverage is guarded at registry construction.
    private NodeViewModelBase CreateNodeViewModel(NodeBase node) => _nodeKinds.CreateViewModel(node, this, _theme);

    /// <summary>Detaches the style coordinator from the shared theme service when the tab closes, so this
    /// VM can be collected, and disposes any resource-holding node view models (e.g. decoded image
    /// bitmaps).</summary>
    public void Dispose()
    {
        _styleCoordinator.Dispose();
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

        RefreshMindMapBranches();
    }

    private static double ConnectorDistance(ConnectorViewModel connector, Point2D world)
        => SegmentGeometry.DistanceToPolyline(world, connector.GetFlattenedPoints());

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
        _alignmentCoordinator.PruneStaleReferences();

        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasNodeSelection));
        OnPropertyChanged(nameof(HasConnectorSelection));
        OnPropertyChanged(nameof(SelectedConnector));
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
