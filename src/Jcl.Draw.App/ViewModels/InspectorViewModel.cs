using System;
using System.Collections.Generic;
using System.Linq;
using ModelStyle = Jcl.Draw.Model.Styling;

namespace Jcl.Draw.App.ViewModels;

/// <summary>Edits text and per-shape styling for the active document's current selection.</summary>
public sealed class InspectorViewModel : ViewModelBase
{
    private DiagramDocumentViewModel? _target;
    private bool _loading;

    public static IReadOnlyList<ModelStyle.TextAlignment> AlignmentOptions { get; } =
        Enum.GetValues<ModelStyle.TextAlignment>();

    public bool HasSelection
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public string Text
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyText(); }
    } = string.Empty;

    public string FillHex
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyFill(); }
    } = "#FFFFFFFF";

    public string StrokeHex
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyStroke(); }
    } = "#FF000000";

    public double StrokeThickness
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyStyle(s => s.Stroke.Thickness = StrokeThickness); }
    } = 1.5d;

    public double FontSize
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyStyle(s => s.Font.Size = FontSize); }
    } = 12d;

    public bool Bold
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyStyle(s => s.Font.Bold = Bold); }
    }

    public bool Italic
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyStyle(s => s.Font.Italic = Italic); }
    }

    public ModelStyle.TextAlignment Alignment
    {
        get;
        set { if (SetProperty(ref field, value)) ApplyStyle(s => s.TextAlignment = Alignment); }
    }

    public void SetTarget(DiagramDocumentViewModel? target)
    {
        if (_target is not null)
        {
            _target.SelectionChanged -= OnSelectionChanged;
        }

        _target = target;

        if (_target is not null)
        {
            _target.SelectionChanged += OnSelectionChanged;
        }

        LoadFromSelection();
    }

    private void OnSelectionChanged(object? sender, EventArgs e) => LoadFromSelection();

    private void LoadFromSelection()
    {
        _loading = true;
        try
        {
            ShapeNodeViewModel? node = _target?.SelectedNodes.FirstOrDefault();
            HasSelection = node is not null;
            if (node is null)
            {
                return;
            }

            ModelStyle.ShapeStyle style = node.Model.Style;
            Text = node.Model.Text;
            FillHex = style.Fill.ToHex();
            StrokeHex = style.Stroke.Color.ToHex();
            StrokeThickness = style.Stroke.Thickness;
            FontSize = style.Font.Size;
            Bold = style.Font.Bold;
            Italic = style.Font.Italic;
            Alignment = style.TextAlignment;
        }
        finally
        {
            _loading = false;
        }
    }

    private void ApplyText()
    {
        if (_loading || _target is null)
        {
            return;
        }

        List<ShapeNodeViewModel> selected = _target.SelectedNodes.ToList();
        if (selected.Count == 0)
        {
            return;
        }

        _target.NotifyStyleEditStarting();
        foreach (ShapeNodeViewModel node in selected)
        {
            node.Text = Text;
        }

        _target.MarkModified();
    }

    private void ApplyFill()
    {
        if (ModelStyle.ArgbColor.TryParse(FillHex, out ModelStyle.ArgbColor color))
        {
            ApplyStyle(s => s.Fill = color);
        }
    }

    private void ApplyStroke()
    {
        if (ModelStyle.ArgbColor.TryParse(StrokeHex, out ModelStyle.ArgbColor color))
        {
            ApplyStyle(s => s.Stroke.Color = color);
        }
    }

    private void ApplyStyle(Action<ModelStyle.ShapeStyle> mutate)
    {
        if (_loading || _target is null)
        {
            return;
        }

        List<ShapeNodeViewModel> selected = _target.SelectedNodes.ToList();
        if (selected.Count == 0)
        {
            return;
        }

        _target.NotifyStyleEditStarting();
        foreach (ShapeNodeViewModel node in selected)
        {
            mutate(node.Model.Style);
            node.RaiseStyleChanged();
        }

        _target.MarkModified();
    }
}
