using System;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;
using Draw.App.Rendering;
using Draw.App.Services;
using Draw.Diagramming.Styling;
using Draw.Model.Nodes;
using Draw.Model.Primitives;
using ModelStyle = Draw.Model.Styling;

namespace Draw.App.ViewModels;

/// <summary>Bindable concerns shared by every node kind: placement, selection and style.</summary>
public abstract class NodeViewModelBase : ViewModelBase
{
    private readonly IThemeService _theme;

    protected NodeViewModelBase(NodeBase model, IThemeService theme)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
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

    /// <summary>When true, resize gestures preserve the node's aspect ratio (e.g. images can't be distorted).</summary>
    public virtual bool LocksAspectRatio => false;

    /// <summary>Stacking order within the node layer; lower renders further back. Bound to the
    /// node container's <c>ZIndex</c> (<c>Visual.ZIndex</c> — Avalonia has no <c>Canvas.ZIndex</c>).
    /// System boundaries are kept in a reserved lower band by the editor's reorder logic (see
    /// <c>DiagramDocumentViewModel.ReorderSelected</c>), so they always sit behind ordinary shapes.</summary>
    public int ZIndex => Model.ZIndex;

    /// <summary>Re-raises <see cref="ZIndex"/> after a stacking-order change so the bound
    /// <c>Visual.ZIndex</c> updates without rebuilding the node collection.</summary>
    public void RaiseZIndexChanged() => OnPropertyChanged(nameof(ZIndex));

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
        set
        {
            if (SetProperty(ref field, value))
            {
                // Selection thickens the node's own outline rather than drawing a separate border.
                OnPropertyChanged(nameof(StrokeThickness));
                OnPropertyChanged(nameof(BorderThickness));
            }
        }
    }

    public bool IsEditing
    {
        get;
        set => SetProperty(ref field, value);
    }

    /// <summary>True when the fill is the un-customised default, so it follows the active theme.</summary>
    public bool UsesDefaultFill => Model.Style.Fill == ModelStyle.ShapeStyle.DefaultFill;

    /// <summary>True when the text colour is the un-customised default, so it follows the active theme.</summary>
    public bool UsesDefaultForeground => Model.Style.Font.Color == ModelStyle.FontSpec.DefaultColor;

    /// <summary>The active-theme variant of the quick-palette swatch this node is linked to, or null
    /// when it carries no <c>PaletteId</c> (custom or default colours).</summary>
    private SwatchVariant? Swatch
        => StylePalette.TryGet(Model.Style.PaletteId, out StyleSwatch swatch) ? swatch.Variant(_theme.IsDark) : null;

    public IBrush Fill => Swatch is { } s ? s.Fill.ToBrush()
        : UsesDefaultFill && _theme.DefaultNodeFill is { } fill ? fill : Model.Style.Fill.ToBrush();

    public IBrush Stroke => Swatch is { } s ? s.Stroke.ToBrush() : Model.Style.Stroke.Color.ToBrush();

    /// <summary>Extra outline width (world units) added while the node is selected. Selection is shown
    /// by thickening the node's own border, so it reads as part of the shape rather than a detached box.</summary>
    private const double SelectedBorderBump = 2d;

    public double StrokeThickness => Model.Style.Stroke.Thickness + (IsSelected ? SelectedBorderBump : 0d);

    /// <summary>Stroke thickness as a uniform <see cref="Thickness"/>, for border-based templates
    /// (class/interface/enum, system boundary). Avalonia has no implicit double→Thickness binding
    /// conversion, so binding the double <see cref="StrokeThickness"/> to a <c>Border.BorderThickness</c>
    /// fails silently and the border vanishes — bind this instead.</summary>
    public Thickness BorderThickness => new(StrokeThickness);

    public AvaloniaList<double>? StrokeDashArray => Model.Style.Stroke.Dash.ToDashArray();

    public IBrush Foreground => Swatch is { } s ? s.Text.ToBrush()
        : UsesDefaultForeground && _theme.DefaultNodeText is { } text ? text : Model.Style.Font.Color.ToBrush();

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
        OnPropertyChanged(nameof(BorderThickness));
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
