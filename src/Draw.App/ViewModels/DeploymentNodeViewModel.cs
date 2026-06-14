using System;
using Avalonia;
using Avalonia.Media;
using Draw.App.Rendering;
using Draw.App.Services;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>Bindable wrapper over a <see cref="DeploymentNode"/>: a 3D box with a name on the front face.</summary>
public sealed class DeploymentNodeViewModel : NodeViewModelBase
{
    private readonly DeploymentNode _model;

    public DeploymentNodeViewModel(DeploymentNode model, IThemeService theme)
        : base(model, theme)
    {
        _model = model;
    }

    public new DeploymentNode Model => _model;

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

    public Geometry Geometry => UmlNodeGeometry.Deployment(Model.Bounds.Width, Model.Bounds.Height);

    /// <summary>Insets the label region to the front face (the back faces occupy the top and right depth).</summary>
    public Thickness FrontFacePadding
    {
        get
        {
            double d = UmlNodeGeometry.DeploymentDepth(Model.Bounds.Width, Model.Bounds.Height);
            return new Thickness(0d, d, d, 0d);
        }
    }

    protected override void OnSizeChanged()
    {
        OnPropertyChanged(nameof(Geometry));
        OnPropertyChanged(nameof(FrontFacePadding));
    }
}
