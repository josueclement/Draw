using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Draw.Model.Connectors;
using Draw.Model.Nodes;
using ModelStyle = Draw.Model.Styling;

namespace Draw.App.ViewModels;

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

    public bool IsShapeSelected
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasNoSelection));
                OnPropertyChanged(nameof(IsNodeSelected));
            }
        }
    }

    public bool IsConnectorSelected
    {
        get;
        private set { if (SetProperty(ref field, value)) OnPropertyChanged(nameof(HasNoSelection)); }
    }

    public bool IsClassNodeSelected
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasNoSelection));
                OnPropertyChanged(nameof(IsNodeSelected));
            }
        }
    }

    public bool IsLabelNodeSelected
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasNoSelection));
                OnPropertyChanged(nameof(IsNodeSelected));
            }
        }
    }

    public bool IsEntityNodeSelected
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasNoSelection));
                OnPropertyChanged(nameof(IsNodeSelected));
            }
        }
    }

    public bool IsNodeSelected => IsLabelNodeSelected || IsClassNodeSelected || IsEntityNodeSelected;

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

    public bool HasNoSelection => !IsLabelNodeSelected && !IsConnectorSelected && !IsClassNodeSelected && !IsEntityNodeSelected;

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
            ConnectorViewModel? connector = _target?.SelectedConnector;
            NodeViewModelBase? node = connector is null ? _target?.SelectedNodes.FirstOrDefault() : null;
            ShapeNodeViewModel? shape = node as ShapeNodeViewModel;
            ClassNodeViewModel? klass = node as ClassNodeViewModel;
            EntityNodeViewModel? entity = node as EntityNodeViewModel;

            IsConnectorSelected = connector is not null;
            IsShapeSelected = shape is not null;
            IsClassNodeSelected = klass is not null;
            IsEntityNodeSelected = entity is not null;
            IsLabelNodeSelected = node is { HasInlineLabel: true };
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
                SourceLabel = model.SourceLabel ?? string.Empty;
                CenterLabel = model.CenterLabel ?? string.Empty;
                TargetLabel = model.TargetLabel ?? string.Empty;
            }
            else if (node is not null)
            {
                ModelStyle.ShapeStyle style = node.Model.Style;
                FillHex = style.Fill.ToHex();
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
        if (_loading || _target?.SelectedConnector is not { } connector)
        {
            return;
        }

        _target.NotifyStyleEditStarting();
        mutate(connector.Model);
        connector.RaiseStyleChanged();
        _target.MarkModified();
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
