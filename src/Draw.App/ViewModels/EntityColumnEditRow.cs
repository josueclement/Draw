using System;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>
/// A single editable table column inside the columns dialog. Wraps a working-copy
/// <see cref="EntityColumn"/> clone — edits stay on the copy until the dialog is saved, so the
/// document model (and undo) is untouched while the dialog is open.
/// </summary>
public sealed class EntityColumnEditRow : ViewModelBase
{
    private readonly EntityColumn _model;

    public EntityColumnEditRow(EntityColumn model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public EntityColumn Model => _model;

    public string Name
    {
        get => _model.Name;
        set
        {
            if (!string.Equals(_model.Name, value, StringComparison.Ordinal))
            {
                _model.Name = value ?? string.Empty;
                OnPropertyChanged();
            }
        }
    }

    public string? Type
    {
        get => _model.Type;
        set
        {
            string? normalized = string.IsNullOrWhiteSpace(value) ? null : value;
            if (!string.Equals(_model.Type, normalized, StringComparison.Ordinal))
            {
                _model.Type = normalized;
                OnPropertyChanged();
            }
        }
    }

    public bool IsPrimaryKey
    {
        get => _model.IsPrimaryKey;
        set
        {
            if (_model.IsPrimaryKey != value)
            {
                _model.IsPrimaryKey = value;
                // A primary key is implicitly NOT NULL, so turning it on also clears nullability.
                if (value && _model.IsNullable)
                {
                    _model.IsNullable = false;
                    OnPropertyChanged(nameof(IsNullable));
                }

                OnPropertyChanged();
            }
        }
    }

    public bool IsForeignKey
    {
        get => _model.IsForeignKey;
        set
        {
            if (_model.IsForeignKey != value)
            {
                _model.IsForeignKey = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsNullable
    {
        get => _model.IsNullable;
        set
        {
            if (_model.IsNullable != value)
            {
                _model.IsNullable = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsUnique
    {
        get => _model.IsUnique;
        set
        {
            if (_model.IsUnique != value)
            {
                _model.IsUnique = value;
                OnPropertyChanged();
            }
        }
    }
}
