using System;
using System.Collections.Generic;
using Avalonia.Media;
using Draw.Diagramming.Er;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>Bindable wrapper over an <see cref="EntityColumn"/>, editable inline (raw text) or field-by-field.</summary>
public sealed class EntityColumnViewModel : ViewModelBase
{
    private readonly EntityColumn _model;
    private readonly INodeEditContext _context;
    private string _editSeed = string.Empty;

    public EntityColumnViewModel(EntityColumn model, INodeEditContext context)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public EntityColumn Model => _model;

    /// <summary>True for a column created blank via the canvas add flow until it is first given a name.</summary>
    public bool IsNewlyAdded { get; set; }

    public IReadOnlyList<string> TypeSuggestions => _context.GetTypeSuggestions();

    public string Name
    {
        get => _model.Name;
        set => Edit(() => _model.Name = value ?? string.Empty, !string.Equals(_model.Name, value, StringComparison.Ordinal));
    }

    public string? Type
    {
        get => _model.Type;
        set => Edit(() => _model.Type = string.IsNullOrWhiteSpace(value) ? null : value, !string.Equals(_model.Type, value, StringComparison.Ordinal));
    }

    public bool IsPrimaryKey
    {
        get => _model.IsPrimaryKey;
        // A primary key is implicitly NOT NULL, so toggling it on also clears nullability.
        set => Edit(
            () =>
            {
                _model.IsPrimaryKey = value;
                if (value)
                {
                    _model.IsNullable = false;
                }
            },
            _model.IsPrimaryKey != value);
    }

    public bool IsForeignKey
    {
        get => _model.IsForeignKey;
        set => Edit(() => _model.IsForeignKey = value, _model.IsForeignKey != value);
    }

    public bool IsNullable
    {
        get => _model.IsNullable;
        set => Edit(() => _model.IsNullable = value, _model.IsNullable != value);
    }

    public bool IsUnique
    {
        get => _model.IsUnique;
        set => Edit(() => _model.IsUnique = value, _model.IsUnique != value);
    }

    public string DisplayText => ColumnSignature.Format(_model);

    /// <summary>A primary key reads as bold; the underline reinforces it as the key row.</summary>
    public FontWeight RowFontWeight => _model.IsPrimaryKey ? FontWeight.Bold : FontWeight.Normal;

    public TextDecorationCollection? RowDecorations => _model.IsPrimaryKey ? TextDecorations.Underline : null;

    public string RawText
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public bool IsEditing
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>
    /// Enters inline editing. A freshly-added column starts blank for clean typing; an existing column
    /// is seeded with its formatted signature so the user can amend it.
    /// </summary>
    public void BeginEdit()
    {
        RawText = IsNewlyAdded ? string.Empty : ColumnSignature.Format(_model);
        _editSeed = RawText;
        IsEditing = true;
    }

    /// <summary>
    /// Parses the edited text back into the model and leaves edit mode. Undo is captured only for a
    /// real change to an already-established column — a newly-added column was already snapshotted when
    /// it was inserted.
    /// </summary>
    public void CommitEdit()
    {
        bool changed = !string.Equals(RawText, _editSeed, StringComparison.Ordinal);
        if (changed)
        {
            bool capture = !IsNewlyAdded;
            if (capture)
            {
                _context.BeginMemberEdit();
            }

            Apply(ColumnSignature.Parse(RawText));
            _context.EndMemberEdit();
        }

        // A column is "established" (no longer disposable) once it has a name.
        if (!string.IsNullOrWhiteSpace(_model.Name))
        {
            IsNewlyAdded = false;
        }

        IsEditing = false;
        RaiseAll();
    }

    public void CancelEdit() => IsEditing = false;

    private void Apply(EntityColumn parsed)
    {
        _model.Name = parsed.Name;
        _model.Type = parsed.Type;
        _model.IsPrimaryKey = parsed.IsPrimaryKey;
        _model.IsForeignKey = parsed.IsForeignKey;
        _model.IsUnique = parsed.IsUnique;
        _model.IsNullable = parsed.IsNullable;
    }

    private void Edit(Action mutate, bool changed)
    {
        if (!changed)
        {
            return;
        }

        _context.BeginMemberEdit();
        mutate();
        _context.EndMemberEdit();
        RaiseAll();
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Type));
        OnPropertyChanged(nameof(IsPrimaryKey));
        OnPropertyChanged(nameof(IsForeignKey));
        OnPropertyChanged(nameof(IsNullable));
        OnPropertyChanged(nameof(IsUnique));
        OnPropertyChanged(nameof(DisplayText));
        OnPropertyChanged(nameof(RowFontWeight));
        OnPropertyChanged(nameof(RowDecorations));
    }
}
