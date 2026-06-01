using System;
using Avalonia.Collections;
using Avalonia.Media;
using Jcl.Draw.App.Rendering;
using Jcl.Draw.Model.Nodes;
using Jcl.Draw.Model.Primitives;

namespace Jcl.Draw.App.ViewModels;

/// <summary>Bindable concerns shared by every node kind: placement, selection and style.</summary>
public abstract class NodeViewModelBase : ViewModelBase
{
    protected NodeViewModelBase(NodeBase model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public NodeBase Model { get; }

    public Guid Id => Model.Id;

    /// <summary>The shape kind used for connector boundary attachment.</summary>
    public abstract ShapeKind BoundaryKind { get; }

    /// <summary>True when this node has a single editable text label (inline + inspector).</summary>
    public virtual bool HasInlineLabel => false;

    /// <summary>The node's single editable label; overridden by label-bearing node kinds.</summary>
    public virtual string Label
    {
        get => string.Empty;
        set { }
    }

    /// <summary>Smallest width/height a resize gesture may produce for this node.</summary>
    public virtual double MinWidth => 12d;

    public virtual double MinHeight => 12d;

    public Rect2D Bounds => Model.Bounds;

    public double X
    {
        get => Model.Bounds.X;
        set
        {
            if (!Model.Bounds.X.Equals(value))
            {
                Model.Bounds = Model.Bounds with { X = value };
                OnPropertyChanged();
            }
        }
    }

    public double Y
    {
        get => Model.Bounds.Y;
        set
        {
            if (!Model.Bounds.Y.Equals(value))
            {
                Model.Bounds = Model.Bounds with { Y = value };
                OnPropertyChanged();
            }
        }
    }

    public double Width
    {
        get => Model.Bounds.Width;
        set
        {
            if (!Model.Bounds.Width.Equals(value))
            {
                Model.Bounds = Model.Bounds with { Width = value };
                OnPropertyChanged();
                OnSizeChanged();
            }
        }
    }

    public double Height
    {
        get => Model.Bounds.Height;
        set
        {
            if (!Model.Bounds.Height.Equals(value))
            {
                Model.Bounds = Model.Bounds with { Height = value };
                OnPropertyChanged();
                OnSizeChanged();
            }
        }
    }

    public bool IsSelected
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool IsEditing
    {
        get;
        set => SetProperty(ref field, value);
    }

    public IBrush Fill => Model.Style.Fill.ToBrush();

    public IBrush Stroke => Model.Style.Stroke.Color.ToBrush();

    public double StrokeThickness => Model.Style.Stroke.Thickness;

    public AvaloniaList<double>? StrokeDashArray => Model.Style.Stroke.Dash.ToDashArray();

    public IBrush Foreground => Model.Style.Font.Color.ToBrush();

    public FontFamily FontFamily => new(Model.Style.Font.Family);

    public double FontSize => Model.Style.Font.Size;

    public FontWeight FontWeight => Model.Style.Font.Bold ? FontWeight.Bold : FontWeight.Normal;

    public FontStyle FontStyle => Model.Style.Font.Italic ? FontStyle.Italic : FontStyle.Normal;

    public TextAlignment TextAlignment => Model.Style.TextAlignment.ToAvalonia();

    /// <summary>Re-raises all style-derived properties after the inspector edits the model style.</summary>
    public virtual void RaiseStyleChanged()
    {
        OnPropertyChanged(nameof(Fill));
        OnPropertyChanged(nameof(Stroke));
        OnPropertyChanged(nameof(StrokeThickness));
        OnPropertyChanged(nameof(StrokeDashArray));
        OnPropertyChanged(nameof(Foreground));
        OnPropertyChanged(nameof(FontFamily));
        OnPropertyChanged(nameof(FontSize));
        OnPropertyChanged(nameof(FontWeight));
        OnPropertyChanged(nameof(FontStyle));
        OnPropertyChanged(nameof(TextAlignment));
    }

    /// <summary>Called when width/height change so subclasses can re-raise size-dependent properties.</summary>
    protected virtual void OnSizeChanged()
    {
    }
}
