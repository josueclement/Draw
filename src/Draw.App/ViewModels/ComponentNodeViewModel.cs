using System;
using Draw.App.Services;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>Bindable wrapper over a <see cref="ComponentNode"/>: a «component» box with port tabs.</summary>
public sealed class ComponentNodeViewModel : NodeViewModelBase
{
    private readonly ComponentNode _model;

    public ComponentNodeViewModel(ComponentNode model, IThemeService theme)
        : base(model, theme)
    {
        _model = model;
    }

    public new ComponentNode Model => _model;

    public override ShapeKind BoundaryKind => ShapeKind.Rectangle;

    public override bool HasInlineLabel => true;

    public override string Label
    {
        get => Name;
        set => Name = value;
    }

    public string Name
    {
        get => _model.Name;
        set
        {
            if (!string.Equals(_model.Name, value, StringComparison.Ordinal))
            {
                _model.Name = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Label));
            }
        }
    }
}
