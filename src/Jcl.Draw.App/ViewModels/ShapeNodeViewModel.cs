using Avalonia.Media;
using Jcl.Draw.App.Rendering;
using Jcl.Draw.Model.Nodes;

namespace Jcl.Draw.App.ViewModels;

/// <summary>Bindable wrapper over a <see cref="ShapeNode"/>; the model is the backing store.</summary>
public sealed class ShapeNodeViewModel : NodeViewModelBase
{
    private readonly ShapeNode _model;

    public ShapeNodeViewModel(ShapeNode model)
        : base(model)
    {
        _model = model;
    }

    public new ShapeNode Model => _model;

    public override ShapeKind BoundaryKind => _model.Kind;

    public override bool HasInlineLabel => true;

    public override string Label
    {
        get => Text;
        set => Text = value;
    }

    public ShapeKind Kind => _model.Kind;

    public string Text
    {
        get => _model.Text;
        set
        {
            if (!string.Equals(_model.Text, value, System.StringComparison.Ordinal))
            {
                _model.Text = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Label));
            }
        }
    }

    public Geometry Geometry
        => ShapeGeometryBuilder.Build(_model.Kind, _model.Bounds.Width, _model.Bounds.Height, _model.CornerRadius);

    protected override void OnSizeChanged() => OnPropertyChanged(nameof(Geometry));
}
