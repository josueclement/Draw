using System;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>
/// A single editable class member inside the members dialog. Wraps a working-copy
/// <see cref="ClassMember"/> clone — edits stay on the copy until the dialog is saved, so the
/// document model (and undo) is untouched while the dialog is open.
/// </summary>
public sealed class ClassMemberEditRow : ViewModelBase
{
    private readonly ClassMember _model;

    public ClassMemberEditRow(ClassMember model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public ClassMember Model => _model;

    public bool IsOperation => _model.Kind == MemberKind.Operation;

    /// <summary>Enum literals carry only a name; fields and operations also expose visibility and a type.</summary>
    public bool ShowDetails => _model.Kind != MemberKind.EnumLiteral;

    public MemberVisibility Visibility
    {
        get => _model.Visibility;
        set
        {
            if (_model.Visibility != value)
            {
                _model.Visibility = value;
                OnPropertyChanged();
            }
        }
    }

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

    public string? Parameters
    {
        get => _model.Parameters;
        set
        {
            if (!string.Equals(_model.Parameters, value, StringComparison.Ordinal))
            {
                _model.Parameters = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsStatic
    {
        get => _model.IsStatic;
        set
        {
            if (_model.IsStatic != value)
            {
                _model.IsStatic = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsAbstract
    {
        get => _model.IsAbstract;
        set
        {
            if (_model.IsAbstract != value)
            {
                _model.IsAbstract = value;
                OnPropertyChanged();
            }
        }
    }
}
