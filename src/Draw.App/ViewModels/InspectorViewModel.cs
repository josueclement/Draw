using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Draw.App.Rendering;
using Draw.Model.Connectors;
using Draw.Model.Nodes;
using ModelStyle = Draw.Model.Styling;

namespace Draw.App.ViewModels;

/// <summary>Which kind of selection the inspector is showing. The booleans the inspector panels bind
/// to are computed from this one value, so a new node kind with an inspector panel is one enum case
/// plus one XAML panel rather than another flag + cast + branch.</summary>
public enum InspectorSelection
{
    None,
    Label,
    ClassNode,
    EntityNode,
    Connector,
}

/// <summary>Edits text/styling for the active document's selection — a shape or a connector.</summary>
public sealed class InspectorViewModel : ViewModelBase
{
    private DiagramDocumentViewModel? _target;
    private bool _loading;

    public static IReadOnlyList<ModelStyle.TextAlignment> AlignmentOptions { get; } =
        Enum.GetValues<ModelStyle.TextAlignment>();

    public static IReadOnlyList<RelationshipKind> RelationshipOptions { get; } =
        Enum.GetValues<RelationshipKind>();

    public static IReadOnlyList<RouteStyle> RouteStyleOptions { get; } =
        Enum.GetValues<RouteStyle>();

    public static IReadOnlyList<MemberVisibility> VisibilityOptions { get; } =
        Enum.GetValues<MemberVisibility>();

    public static IReadOnlyList<Cardinality> CardinalityOptions { get; } =
        Enum.GetValues<Cardinality>();

    public static IReadOnlyList<ModelStyle.DashStyle> DashStyleOptions { get; } =
        Enum.GetValues<ModelStyle.DashStyle>();

    /// <summary>The current selection kind. The per-kind booleans below are computed from this, so
    /// LoadFromSelection sets exactly one value instead of a fan of flags.</summary>
    public InspectorSelection SelectionKind
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(IsConnectorSelected));
                OnPropertyChanged(nameof(IsClassNodeSelected));
                OnPropertyChanged(nameof(IsEntityNodeSelected));
                OnPropertyChanged(nameof(IsLabelNodeSelected));
                OnPropertyChanged(nameof(IsNodeSelected));
                OnPropertyChanged(nameof(HasNoSelection));
            }
        }
    }

    public bool IsConnectorSelected => SelectionKind == InspectorSelection.Connector;

    /// <summary>True only when exactly one connector is selected — gates the per-connector fields
    /// (cardinality, labels) that have no meaningful shared value across a multi-connector selection.
    /// A finer gate than <see cref="IsConnectorSelected"/>, so it stays a separate flag.</summary>
    public bool IsSingleConnectorSelected
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public bool IsClassNodeSelected => SelectionKind == InspectorSelection.ClassNode;

    public bool IsLabelNodeSelected => SelectionKind == InspectorSelection.Label;

    public bool IsEntityNodeSelected => SelectionKind == InspectorSelection.EntityNode;

    public bool IsNodeSelected =>
        SelectionKind is InspectorSelection.Label or InspectorSelection.ClassNode or InspectorSelection.EntityNode;

    public ClassNodeViewModel? SelectedClassNode
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public EntityNodeViewModel? SelectedEntityNode
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public bool HasNoSelection => SelectionKind == InspectorSelection.None;

    /// <summary>The status-marker toggles shown for a node selection (one per <see cref="NodeMarker"/>).
    /// Built once; their checked state is refreshed from the selection in <see cref="LoadFromSelection"/>.</summary>
    public IReadOnlyList<MarkerToggleViewModel> MarkerToggles { get; }

    // --- Shape properties ---

    public string Text
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyText(); }
    } = string.Empty;

    public string FillHex
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyFill(); }
    } = "#FFFFFFFF";

    public string StrokeHex
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyShapeStroke(); }
    } = "#FF000000";

    public double StrokeThickness
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyShapeStyle(s => s.Stroke.Thickness = StrokeThickness); }
    } = 1.5d;

    public double FontSize
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyShapeStyle(s => s.Font.Size = FontSize); }
    } = 12d;

    public bool Bold
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyShapeStyle(s => s.Font.Bold = Bold); }
    }

    public bool Italic
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyShapeStyle(s => s.Font.Italic = Italic); }
    }

    public ModelStyle.TextAlignment Alignment
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyShapeStyle(s => s.TextAlignment = Alignment); }
    }

    // --- Connector properties ---

    public RelationshipKind ConnectorKind
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyConnector(c => c.Kind = ConnectorKind); }
    }

    public RouteStyle ConnectorRouteStyle
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyConnector(c => c.Route = ConnectorRouteStyle); }
    }

    public Cardinality SourceCardinality
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyConnector(c => c.SourceCardinality = SourceCardinality); }
    }

    public Cardinality TargetCardinality
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyConnector(c => c.TargetCardinality = TargetCardinality); }
    }

    public string ConnectorStrokeHex
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyConnectorStroke(); }
    } = "#FF000000";

    public double ConnectorThickness
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyConnector(c => c.Style.Stroke.Thickness = ConnectorThickness); }
    } = 1.5d;

    public ModelStyle.DashStyle ConnectorDash
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyConnector(c => c.Style.Stroke.Dash = ConnectorDash); }
    }

    public string SourceLabel
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyConnector(c => c.SourceLabel = NullIfEmpty(SourceLabel)); }
    } = string.Empty;

    public string CenterLabel
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyConnector(c => c.CenterLabel = NullIfEmpty(CenterLabel)); }
    } = string.Empty;

    public string TargetLabel
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyConnector(c => c.TargetLabel = NullIfEmpty(TargetLabel)); }
    } = string.Empty;

    // --- Class member-editor commands ---

    public IRelayCommand AddPrimaryMemberCommand { get; }

    public IRelayCommand AddOperationCommand { get; }

    public IRelayCommand<ClassMemberViewModel> RemoveMemberCommand { get; }

    public IRelayCommand<ClassMemberViewModel> MoveMemberUpCommand { get; }

    public IRelayCommand<ClassMemberViewModel> MoveMemberDownCommand { get; }

    // --- Entity column-editor commands ---

    public IRelayCommand AddColumnCommand { get; }

    public IRelayCommand<EntityColumnViewModel> RemoveColumnCommand { get; }

    public IRelayCommand<EntityColumnViewModel> MoveColumnUpCommand { get; }

    public IRelayCommand<EntityColumnViewModel> MoveColumnDownCommand { get; }

    public InspectorViewModel()
    {
        AddPrimaryMemberCommand = new RelayCommand(() => SelectedClassNode?.AddPrimaryMember());
        AddOperationCommand = new RelayCommand(() => SelectedClassNode?.AddOperation());
        RemoveMemberCommand = new RelayCommand<ClassMemberViewModel>(m => { if (m is not null) SelectedClassNode?.RemoveMember(m); });
        MoveMemberUpCommand = new RelayCommand<ClassMemberViewModel>(m => { if (m is not null) SelectedClassNode?.MoveMember(m, -1); });
        MoveMemberDownCommand = new RelayCommand<ClassMemberViewModel>(m => { if (m is not null) SelectedClassNode?.MoveMember(m, +1); });

        AddColumnCommand = new RelayCommand(() => SelectedEntityNode?.AddColumn());
        RemoveColumnCommand = new RelayCommand<EntityColumnViewModel>(c => { if (c is not null) SelectedEntityNode?.RemoveColumn(c); });
        MoveColumnUpCommand = new RelayCommand<EntityColumnViewModel>(c => { if (c is not null) SelectedEntityNode?.MoveColumn(c, -1); });
        MoveColumnDownCommand = new RelayCommand<EntityColumnViewModel>(c => { if (c is not null) SelectedEntityNode?.MoveColumn(c, +1); });

        MarkerToggles = NodeMarkerVisuals.Order
            .Select(marker => new MarkerToggleViewModel(this, NodeMarkerVisuals.For(marker)))
            .ToList();
    }

    /// <summary>Applies a marker toggle to the whole node selection (one undo step) when the user flips it.</summary>
    internal void OnMarkerToggleChanged(MarkerToggleViewModel toggle)
    {
        if (_loading || _target is null)
        {
            return;
        }

        _target.ToggleNodeMarker(toggle.Marker);
        RefreshMarkerToggles();
    }

    private void RefreshMarkerToggles()
    {
        foreach (MarkerToggleViewModel toggle in MarkerToggles)
        {
            toggle.Refresh(_target?.SelectionHasMarker(toggle.Marker) ?? false);
        }
    }

    public void SetTarget(DiagramDocumentViewModel? target)
    {
        if (_target is not null)
        {
            _target.SelectionChanged -= OnSelectionChanged;
        }

        _target = target;

        if (_target is not null)
        {
            _target.SelectionChanged += OnSelectionChanged;
        }

        LoadFromSelection();
    }

    private void OnSelectionChanged(object? sender, EventArgs e) => LoadFromSelection();

    private void LoadFromSelection()
    {
        _loading = true;
        try
        {
            // Connector-priority: when any connector is selected the inspector shows the connector panel
            // (representative = the first selected connector). Per-connector fields are gated separately
            // on a single-connector selection. Node styling in a mixed grab is done via the palette.
            List<ConnectorViewModel> connectors = _target?.SelectedConnectors.ToList() ?? new List<ConnectorViewModel>();
            ConnectorViewModel? connector = connectors.Count > 0 ? connectors[0] : null;
            NodeViewModelBase? node = connector is null ? _target?.SelectedNodes.FirstOrDefault() : null;
            ClassNodeViewModel? klass = node as ClassNodeViewModel;
            EntityNodeViewModel? entity = node as EntityNodeViewModel;

            // Exactly one kind: connector takes priority; among nodes, class/entity have dedicated panels
            // and everything else with an inline label (shape/actor/use-case/boundary) is "Label".
            SelectionKind = connector is not null ? InspectorSelection.Connector
                : klass is not null ? InspectorSelection.ClassNode
                : entity is not null ? InspectorSelection.EntityNode
                : node is { HasInlineLabel: true } ? InspectorSelection.Label
                : InspectorSelection.None;
            IsSingleConnectorSelected = _target?.SelectedConnector is not null;
            SelectedClassNode = klass;
            SelectedEntityNode = entity;

            if (connector is not null)
            {
                Connector model = connector.Model;
                ConnectorKind = model.Kind;
                ConnectorRouteStyle = model.Route;
                SourceCardinality = model.SourceCardinality;
                TargetCardinality = model.TargetCardinality;
                ConnectorStrokeHex = model.Style.Stroke.Color.ToHex();
                ConnectorThickness = model.Style.Stroke.Thickness;
                ConnectorDash = model.Style.Stroke.Dash;
                SourceLabel = model.SourceLabel ?? string.Empty;
                CenterLabel = model.CenterLabel ?? string.Empty;
                TargetLabel = model.TargetLabel ?? string.Empty;
            }
            else if (node is not null)
            {
                ModelStyle.ShapeStyle style = node.Model.Style;
                // A null fill follows the theme; show the concrete default in the picker so the field
                // isn't blank (editing it writes a concrete colour, detaching from the theme default).
                FillHex = (style.Fill ?? ModelStyle.ShapeStyle.DefaultFill).ToHex();
                StrokeHex = style.Stroke.Color.ToHex();
                StrokeThickness = style.Stroke.Thickness;
                FontSize = style.Font.Size;
                Bold = style.Font.Bold;
                Italic = style.Font.Italic;
                Alignment = style.TextAlignment;

                if (node.HasInlineLabel)
                {
                    Text = node.Label;
                }
            }

            RefreshMarkerToggles();
        }
        finally
        {
            _loading = false;
        }
    }

    private void ApplyText()
    {
        if (_loading || _target is null)
        {
            return;
        }

        List<NodeViewModelBase> selected = _target.SelectedNodes.Where(n => n.HasInlineLabel).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        _target.NotifyStyleEditStarting();
        foreach (NodeViewModelBase node in selected)
        {
            node.Label = Text;
        }

        _target.MarkModified();
    }

    private void ApplyFill()
    {
        // A hand-edited colour detaches the shape from its quick-palette swatch (it stops following the
        // theme and becomes a custom colour).
        if (ModelStyle.ArgbColor.TryParse(FillHex, out ModelStyle.ArgbColor color))
        {
            ApplyShapeStyle(s => { s.Fill = color; s.PaletteId = null; });
        }
    }

    private void ApplyShapeStroke()
    {
        if (ModelStyle.ArgbColor.TryParse(StrokeHex, out ModelStyle.ArgbColor color))
        {
            ApplyShapeStyle(s => { s.Stroke.Color = color; s.PaletteId = null; });
        }
    }

    private void ApplyShapeStyle(Action<ModelStyle.ShapeStyle> mutate)
    {
        if (_loading || _target is null)
        {
            return;
        }

        List<NodeViewModelBase> selected = _target.SelectedNodes.ToList();
        if (selected.Count == 0)
        {
            return;
        }

        _target.NotifyStyleEditStarting();
        foreach (NodeViewModelBase node in selected)
        {
            mutate(node.Model.Style);
            node.RaiseStyleChanged();
        }

        _target.MarkModified();
    }

    private void ApplyConnectorStroke()
    {
        if (ModelStyle.ArgbColor.TryParse(ConnectorStrokeHex, out ModelStyle.ArgbColor color))
        {
            ApplyConnector(c => { c.Style.Stroke.Color = color; c.Style.PaletteId = null; });
        }
    }

    private void ApplyConnector(Action<Connector> mutate)
    {
        if (_loading || _target is null)
        {
            return;
        }

        List<ConnectorViewModel> connectors = _target.SelectedConnectors.ToList();
        if (connectors.Count == 0)
        {
            return;
        }

        _target.NotifyStyleEditStarting();
        foreach (ConnectorViewModel connector in connectors)
        {
            mutate(connector.Model);
            connector.RaiseStyleChanged();
        }

        _target.MarkModified();
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

/// <summary>One status-marker toggle in the inspector: its icon/colour/label plus a two-way checked state
/// that, when flipped by the user, applies the marker to the whole node selection via the owning inspector.</summary>
public sealed class MarkerToggleViewModel : ViewModelBase
{
    private readonly InspectorViewModel _owner;
    private bool _isSet;

    public MarkerToggleViewModel(InspectorViewModel owner, NodeMarkerVisual visual)
    {
        _owner = owner;
        Marker = visual.Marker;
        Icon = visual.Icon;
        Brush = visual.Brush;
        Label = visual.Label;
    }

    public NodeMarker Marker { get; }

    public Geometry Icon { get; }

    public IBrush Brush { get; }

    public string Label { get; }

    public bool IsSet
    {
        get => _isSet;
        set
        {
            if (_isSet != value)
            {
                _isSet = value;
                OnPropertyChanged();
                _owner.OnMarkerToggleChanged(this);
            }
        }
    }

    /// <summary>Sets the displayed checked state from the current selection without re-applying to the model.</summary>
    public void Refresh(bool value)
    {
        if (_isSet != value)
        {
            _isSet = value;
            OnPropertyChanged(nameof(IsSet));
        }
    }
}
