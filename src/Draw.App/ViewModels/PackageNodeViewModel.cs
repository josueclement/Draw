using System;
using Draw.App.Services;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>Bindable wrapper over a <see cref="PackageNode"/>: a folder-tab box with a title.</summary>
public sealed class PackageNodeViewModel : NodeViewModelBase
{
    private readonly PackageNode _model;

    public PackageNodeViewModel(PackageNode model, IThemeService theme)
        : base(model, theme)
    {
        _model = model;
    }

    public new PackageNode Model => _model;

    public override ShapeKind BoundaryKind => ShapeKind.Rectangle;

    public override bool HasInlineLabel => true;

    public override string Label
    {
        get => Title;
        set => Title = value;
    }

    public string Title
    {
        get => _model.Title;
        set
        {
            if (!string.Equals(_model.Title, value, StringComparison.Ordinal))
            {
                _model.Title = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Label));
            }
        }
    }

    /// <summary>Width of the folder tab (a fraction of the node width).</summary>
    public double TabWidth => Math.Min(Width * 0.45d, Math.Max(Width - 4d, 0d));

    /// <summary>Height of the folder tab (capped so it stays a tab on short packages).</summary>
    public double TabHeight => Math.Min(Height * 0.28d, 22d);

    protected override void OnSizeChanged()
    {
        OnPropertyChanged(nameof(TabWidth));
        OnPropertyChanged(nameof(TabHeight));
    }
}
