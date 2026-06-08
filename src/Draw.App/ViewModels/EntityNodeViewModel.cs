using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Draw.App.Services;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>Bindable wrapper over an <see cref="EntityNode"/>: a table name over a flat column list.</summary>
public sealed class EntityNodeViewModel : NodeViewModelBase
{
    private const double NameCompartmentHeight = 28d;
    private const double ColumnRowHeight = 18d;
    private const double CompartmentPadding = 8d;

    private readonly EntityNode _model;
    private readonly INodeEditContext _context;

    public EntityNodeViewModel(EntityNode model, INodeEditContext context, IThemeService theme)
        : base(model, theme)
    {
        _model = model;
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Columns = new ObservableCollection<EntityColumnViewModel>(_model.Columns.Select(Wrap));
    }

    public new EntityNode Model => _model;

    public override ShapeKind BoundaryKind => ShapeKind.Rectangle;

    public ObservableCollection<EntityColumnViewModel> Columns { get; }

    public string Name
    {
        get => _model.Name;
        set
        {
            if (!string.Equals(_model.Name, value, StringComparison.Ordinal))
            {
                _context.BeginMemberEdit();
                _model.Name = value ?? string.Empty;
                _context.EndMemberEdit();
                OnPropertyChanged();
            }
        }
    }

    public override double MinHeight => NameCompartmentHeight + (Columns.Count * ColumnRowHeight) + CompartmentPadding;

    public override double MinWidth => 100d;

    public void AddColumn() => AddColumnModel(new EntityColumn { Name = "column" });

    /// <summary>
    /// Inserts a blank column at <paramref name="index"/> (clamped; negative ⇒ append), enters inline
    /// editing on it and returns its view model so the caller can focus the editor.
    /// </summary>
    public EntityColumnViewModel InsertNewColumn(int index)
    {
        _context.BeginMemberEdit();

        EntityColumn column = new();
        EntityColumnViewModel vm = Wrap(column);
        vm.IsNewlyAdded = true;

        int target = index < 0 || index > Columns.Count ? Columns.Count : index;
        Columns.Insert(target, vm);
        ReorderModelFromCollection();

        vm.BeginEdit();
        _context.EndMemberEdit();
        GrowToFitContent();
        OnPropertyChanged(nameof(MinHeight));
        return vm;
    }

    /// <summary>Finds the index of <paramref name="column"/> (index -1 if absent).</summary>
    public (ObservableCollection<EntityColumnViewModel> List, int Index) Locate(EntityColumnViewModel column)
        => (Columns, Columns.IndexOf(column));

    /// <summary>
    /// Removes any still-unnamed columns created via the add flow (e.g. a trailing blank left by
    /// Enter-then-Escape). Silent: the original insert already captured undo and marked dirty.
    /// </summary>
    public void DiscardEmptyNewColumns()
    {
        List<EntityColumnViewModel> blanks = Columns
            .Where(c => c.IsNewlyAdded && string.IsNullOrWhiteSpace(c.Name))
            .ToList();
        if (blanks.Count == 0)
        {
            return;
        }

        foreach (EntityColumnViewModel c in blanks)
        {
            _model.Columns.Remove(c.Model);
            Columns.Remove(c);
        }

        OnPropertyChanged(nameof(MinHeight));
    }

    public void RemoveColumn(EntityColumnViewModel column)
    {
        if (column is null)
        {
            return;
        }

        _context.BeginMemberEdit();
        _model.Columns.Remove(column.Model);
        Columns.Remove(column);
        _context.EndMemberEdit();
        OnPropertyChanged(nameof(MinHeight));
    }

    /// <summary>
    /// Replaces the whole column set in one gesture (used by the modal editor's Save). The supplied
    /// columns are cloned so the editor's working copies stay detached from the document; the swap is
    /// captured as a single undo step.
    /// </summary>
    public void ReplaceColumns(IReadOnlyList<EntityColumn> columns)
    {
        _context.BeginMemberEdit();

        Columns.Clear();
        foreach (EntityColumn column in columns)
        {
            Columns.Add(Wrap(column.Clone()));
        }

        ReorderModelFromCollection();
        _context.EndMemberEdit();
        GrowToFitContent();
        OnPropertyChanged(nameof(MinHeight));
    }

    public void MoveColumn(EntityColumnViewModel column, int delta)
    {
        int index = Columns.IndexOf(column);
        int target = index + delta;
        if (index < 0 || target < 0 || target >= Columns.Count)
        {
            return;
        }

        _context.BeginMemberEdit();
        Columns.Move(index, target);
        ReorderModelFromCollection();
        _context.EndMemberEdit();
    }

    /// <summary>Commits any column row currently being edited (used by the Escape/blur path).</summary>
    public bool CommitPendingEdits()
    {
        bool any = false;
        foreach (EntityColumnViewModel c in Columns)
        {
            if (c.IsEditing)
            {
                c.CommitEdit();
                any = true;
            }
        }

        return any;
    }

    private void AddColumnModel(EntityColumn column)
    {
        _context.BeginMemberEdit();
        _model.Columns.Add(column);
        Columns.Add(Wrap(column));
        _context.EndMemberEdit();
        GrowToFitContent();
        OnPropertyChanged(nameof(MinHeight));
    }

    private void GrowToFitContent()
    {
        if (Height < MinHeight)
        {
            Height = MinHeight; // base setter writes through to the model bounds
        }
    }

    private void ReorderModelFromCollection()
    {
        _model.Columns.Clear();
        foreach (EntityColumnViewModel c in Columns)
        {
            _model.Columns.Add(c.Model);
        }
    }

    private EntityColumnViewModel Wrap(EntityColumn c) => new(c, _context);
}
