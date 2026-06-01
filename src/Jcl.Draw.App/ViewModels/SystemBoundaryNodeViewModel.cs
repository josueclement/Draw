using System;
using Jcl.Draw.Model.Nodes;

namespace Jcl.Draw.App.ViewModels;

/// <summary>Bindable wrapper over a <see cref="SystemBoundaryNode"/>: a titled box drawn behind.</summary>
public sealed class SystemBoundaryNodeViewModel : NodeViewModelBase
{
    private readonly SystemBoundaryNode _model;

    public SystemBoundaryNodeViewModel(SystemBoundaryNode model)
        : base(model)
    {
        _model = model;
    }

    public new SystemBoundaryNode Model => _model;

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
}
