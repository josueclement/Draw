using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Jcl.Draw.App.Configuration;
using Jcl.Draw.Diagramming.Geometry;
using Jcl.Draw.Diagramming.Undo;
using Jcl.Draw.Model.Documents;
using Jcl.Draw.Model.Nodes;
using Jcl.Draw.Model.Primitives;

namespace Jcl.Draw.App.ViewModels;

/// <summary>Editor state and mutating operations for one open diagram (one tab).</summary>
public sealed class DiagramDocumentViewModel : ViewModelBase
{
    private readonly IUndoService _undo;
    private readonly EditorOptions _options;
    private DiagramDocument _document;

    public DiagramDocumentViewModel(DiagramDocument document, IUndoService undo, EditorOptions options, string? filePath)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _undo = undo ?? throw new ArgumentNullException(nameof(undo));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        FilePath = filePath;
        _undo.StateChanged += (_, _) => RaiseUndoState();
        RebuildNodes();
    }

    public ObservableCollection<ShapeNodeViewModel> Nodes { get; } = new();

    public DiagramDocument Document => _document;

    public DiagramType DiagramType => _document.DiagramType;

    public double GridSize => _options.GridSize;

    public bool SnapEnabled => _options.SnapToGrid;

    public string? FilePath
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public bool IsModified
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public string DisplayName
    {
        get
        {
            string name = FilePath is null ? "Untitled" : Path.GetFileName(FilePath);
            return IsModified ? name + " *" : name;
        }
    }

    public double Zoom
    {
        get;
        set => SetProperty(ref field, value);
    } = 1d;

    public double PanX
    {
        get;
        set => SetProperty(ref field, value);
    }

    public double PanY
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool CanUndo => _undo.CanUndo;

    public bool CanRedo => _undo.CanRedo;

    public bool HasSelection => Nodes.Any(n => n.IsSelected);

    public IEnumerable<ShapeNodeViewModel> SelectedNodes => Nodes.Where(n => n.IsSelected);

    public event EventHandler? UndoStateChanged;

    public event EventHandler? SelectionChanged;

    /// <summary>Snapshots current state before a mutation. Call once at the start of a gesture.</summary>
    public void CaptureUndo() => _undo.Capture(_document);

    public ShapeNodeViewModel AddShape(ShapeKind kind, Point2D center)
    {
        CaptureUndo();

        double w = _options.DefaultShapeWidth;
        double h = _options.DefaultShapeHeight;
        Rect2D bounds = new(center.X - (w / 2), center.Y - (h / 2), w, h);
        if (SnapEnabled)
        {
            bounds = bounds.PositionSnappedToGrid(GridSize);
        }

        ShapeNode node = new()
        {
            Kind = kind,
            Bounds = bounds,
            Style = _document.DefaultShapeStyle.Clone(),
            ZIndex = NextZIndex(),
        };

        _document.Nodes.Add(node);
        ShapeNodeViewModel vm = new(node);
        Nodes.Add(vm);
        SelectOnly(vm);
        MarkModified();
        return vm;
    }

    public void DeleteSelected()
    {
        List<ShapeNodeViewModel> selected = SelectedNodes.ToList();
        if (selected.Count == 0)
        {
            return;
        }

        CaptureUndo();
        foreach (ShapeNodeViewModel vm in selected)
        {
            _document.Nodes.Remove(vm.Model);
            Nodes.Remove(vm);
        }

        MarkModified();
        RaiseSelectionChanged();
    }

    public void MoveSelectedBy(double dx, double dy)
    {
        foreach (ShapeNodeViewModel vm in SelectedNodes)
        {
            vm.X += dx;
            vm.Y += dy;
        }

        MarkModified();
    }

    public void SnapSelectionToGrid()
    {
        if (!SnapEnabled)
        {
            return;
        }

        foreach (ShapeNodeViewModel vm in SelectedNodes)
        {
            Rect2D snapped = vm.Model.Bounds.PositionSnappedToGrid(GridSize);
            vm.X = snapped.X;
            vm.Y = snapped.Y;
        }
    }

    public void SetNodeBounds(ShapeNodeViewModel vm, Rect2D bounds)
    {
        vm.X = bounds.X;
        vm.Y = bounds.Y;
        vm.Width = bounds.Width;
        vm.Height = bounds.Height;
        MarkModified();
    }

    public void SelectOnly(ShapeNodeViewModel vm)
    {
        foreach (ShapeNodeViewModel n in Nodes)
        {
            n.IsSelected = ReferenceEquals(n, vm);
        }

        RaiseSelectionChanged();
    }

    public void ToggleSelect(ShapeNodeViewModel vm)
    {
        vm.IsSelected = !vm.IsSelected;
        RaiseSelectionChanged();
    }

    public void ClearSelection()
    {
        foreach (ShapeNodeViewModel n in Nodes)
        {
            n.IsSelected = false;
        }

        RaiseSelectionChanged();
    }

    public void SelectInRect(Rect2D rect, bool additive)
    {
        foreach (ShapeNodeViewModel n in Nodes)
        {
            if (!additive)
            {
                n.IsSelected = false;
            }

            if (rect.IntersectsWith(n.Model.Bounds))
            {
                n.IsSelected = true;
            }
        }

        RaiseSelectionChanged();
    }

    public void Undo()
    {
        _document = _undo.Undo(_document);
        RebuildNodes();
        MarkModified();
        RaiseUndoState();
    }

    public void Redo()
    {
        _document = _undo.Redo(_document);
        RebuildNodes();
        MarkModified();
        RaiseUndoState();
    }

    public void MarkModified() => IsModified = true;

    public void MarkSaved(string path)
    {
        FilePath = path;
        IsModified = false;
    }

    public void NotifyStyleEditStarting() => CaptureUndo();

    private int NextZIndex() => _document.Nodes.Count == 0 ? 0 : _document.Nodes.Max(n => n.ZIndex) + 1;

    private void RebuildNodes()
    {
        Nodes.Clear();
        foreach (ShapeNode node in _document.Nodes.OfType<ShapeNode>().OrderBy(n => n.ZIndex))
        {
            Nodes.Add(new ShapeNodeViewModel(node));
        }

        RaiseSelectionChanged();
    }

    private void RaiseUndoState()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        UndoStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseSelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelection));
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
