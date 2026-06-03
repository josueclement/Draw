using System;
using Avalonia.Media;
using Draw.App.Rendering;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>Bindable wrapper over an <see cref="ActorNode"/>: stick figure + name label.</summary>
public sealed class ActorNodeViewModel : NodeViewModelBase
{
    private readonly ActorNode _model;

    public ActorNodeViewModel(ActorNode model)
        : base(model)
    {
        _model = model;
    }

    public new ActorNode Model => _model;

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

    public Geometry Geometry => ActorGeometry.Build(Model.Bounds.Width, Model.Bounds.Height);

    protected override void OnSizeChanged() => OnPropertyChanged(nameof(Geometry));
}
