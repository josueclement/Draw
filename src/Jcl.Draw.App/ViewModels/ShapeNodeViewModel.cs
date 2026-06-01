using System;
using Avalonia.Collections;
using Avalonia.Media;
using Jcl.Draw.App.Rendering;
using Jcl.Draw.Model.Nodes;

namespace Jcl.Draw.App.ViewModels;

/// <summary>Bindable wrapper over a <see cref="ShapeNode"/>; the model is the backing store.</summary>
public sealed class ShapeNodeViewModel : ViewModelBase
{
    private readonly ShapeNode _model;

    public ShapeNodeViewModel(ShapeNode model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public ShapeNode Model => _model;

    public Guid Id => _model.Id;

    public ShapeKind Kind => _model.Kind;

    public double X
    {
        get => _model.Bounds.X;
        set
        {
            if (!_model.Bounds.X.Equals(value))
            {
                _model.Bounds = _model.Bounds with { X = value };
                OnPropertyChanged();
            }
        }
    }

    public double Y
    {
        get => _model.Bounds.Y;
        set
        {
            if (!_model.Bounds.Y.Equals(value))
            {
                _model.Bounds = _model.Bounds with { Y = value };
                OnPropertyChanged();
            }
        }
    }

    public double Width
    {
        get => _model.Bounds.Width;
        set
        {
            if (!_model.Bounds.Width.Equals(value))
            {
                _model.Bounds = _model.Bounds with { Width = value };
                OnPropertyChanged();
                OnPropertyChanged(nameof(Geometry));
            }
        }
    }

    public double Height
    {
        get => _model.Bounds.Height;
        set
        {
            if (!_model.Bounds.Height.Equals(value))
            {
                _model.Bounds = _model.Bounds with { Height = value };
                OnPropertyChanged();
                OnPropertyChanged(nameof(Geometry));
            }
        }
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
            }
        }
    }

    public bool IsSelected
    {
        get;
        set => SetProperty(ref field, value);
    }

    /// <summary>True while the node's text is being edited in place on the canvas.</summary>
    public bool IsEditing
    {
        get;
        set => SetProperty(ref field, value);
    }

    public Geometry Geometry
        => ShapeGeometryBuilder.Build(_model.Kind, _model.Bounds.Width, _model.Bounds.Height, _model.CornerRadius);

    public IBrush Fill => _model.Style.Fill.ToBrush();

    public IBrush Stroke => _model.Style.Stroke.Color.ToBrush();

    public double StrokeThickness => _model.Style.Stroke.Thickness;

    public AvaloniaList<double>? StrokeDashArray => _model.Style.Stroke.Dash.ToDashArray();

    public IBrush Foreground => _model.Style.Font.Color.ToBrush();

    public FontFamily FontFamily => new(_model.Style.Font.Family);

    public double FontSize => _model.Style.Font.Size;

    public FontWeight FontWeight => _model.Style.Font.Bold ? FontWeight.Bold : FontWeight.Normal;

    public FontStyle FontStyle => _model.Style.Font.Italic ? FontStyle.Italic : FontStyle.Normal;

    public TextAlignment TextAlignment => _model.Style.TextAlignment.ToAvalonia();

    /// <summary>Re-raises all style-derived properties after the inspector edits the model style.</summary>
    public void RaiseStyleChanged()
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
}
