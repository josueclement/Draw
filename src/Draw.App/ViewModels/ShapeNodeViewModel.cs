using System.ComponentModel;
using Avalonia.Media;
using Draw.App.Rendering;
using Draw.App.Services;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>Bindable wrapper over a <see cref="ShapeNode"/>; the model is the backing store.</summary>
public sealed class ShapeNodeViewModel : NodeViewModelBase
{
    private readonly ShapeNode _model;

    public ShapeNodeViewModel(ShapeNode model, IThemeService theme)
        : base(model, theme)
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

    /// <summary>True for mind-map topics: the node shows a hover '+' on each side that spawns a
    /// linked, tapered child.</summary>
    public bool IsMindMap => _model.Kind is ShapeKind.MindMapTopic or ShapeKind.MindMapTopicRounded;

    /// <summary>The hover '+' child buttons show only on mind-map topics, and never while the inline
    /// text editor is open (so they don't overlap or steal clicks from the editor).</summary>
    public bool ShowChildButtons => IsMindMap && !IsEditing;

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

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(IsEditing))
        {
            OnPropertyChanged(nameof(ShowChildButtons));
        }
    }
}
