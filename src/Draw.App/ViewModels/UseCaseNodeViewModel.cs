using System;
using Avalonia.Media;
using Draw.App.Rendering;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>Bindable wrapper over a <see cref="UseCaseNode"/>: ellipse + centered text.</summary>
public sealed class UseCaseNodeViewModel : NodeViewModelBase
{
    private readonly UseCaseNode _model;

    public UseCaseNodeViewModel(UseCaseNode model)
        : base(model)
    {
        _model = model;
    }

    public new UseCaseNode Model => _model;

    public override ShapeKind BoundaryKind => ShapeKind.Ellipse;

    public override bool HasInlineLabel => true;

    public override string Label
    {
        get => Text;
        set => Text = value;
    }

    public string Text
    {
        get => _model.Text;
        set
        {
            if (!string.Equals(_model.Text, value, StringComparison.Ordinal))
            {
                _model.Text = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Label));
            }
        }
    }

    public Geometry Geometry
        => ShapeGeometryBuilder.Build(ShapeKind.Ellipse, Model.Bounds.Width, Model.Bounds.Height, 0d);

    protected override void OnSizeChanged() => OnPropertyChanged(nameof(Geometry));
}
