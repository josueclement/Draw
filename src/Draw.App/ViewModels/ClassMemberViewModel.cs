using System;
using System.Collections.Generic;
using Avalonia.Media;
using Draw.Diagramming.Uml;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>Bindable wrapper over a <see cref="ClassMember"/>, editable inline (raw text) or field-by-field.</summary>
public sealed class ClassMemberViewModel : ViewModelBase
{
    private readonly ClassMember _model;
    private readonly INodeEditContext _context;
    private string _editSeed = string.Empty;

    public ClassMemberViewModel(ClassMember model, INodeEditContext context)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public ClassMember Model => _model;

    /// <summary>True for a member created blank via the canvas add flow until it is first given a name.</summary>
    public bool IsNewlyAdded { get; set; }

    public bool IsEnumLiteral => _model.Kind == MemberKind.EnumLiteral;

    public IReadOnlyList<string> TypeSuggestions => _context.GetTypeSuggestions();

    public MemberVisibility Visibility
    {
        get => _model.Visibility;
        set => Edit(() => _model.Visibility = value, _model.Visibility != value);
    }

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

    public string? Parameters
    {
        get => _model.Parameters;
        set => Edit(() => _model.Parameters = value, !string.Equals(_model.Parameters, value, StringComparison.Ordinal));
    }

    public bool IsStatic
    {
        get => _model.IsStatic;
        set => Edit(() => _model.IsStatic = value, _model.IsStatic != value);
    }

    public bool IsAbstract
    {
        get => _model.IsAbstract;
        set => Edit(() => _model.IsAbstract = value, _model.IsAbstract != value);
    }

    public bool IsOperation => _model.Kind == MemberKind.Operation;

    public string DisplayText => MemberSignature.Format(_model);

    public FontStyle RowFontStyle => _model.IsAbstract ? FontStyle.Italic : FontStyle.Normal;

    public TextDecorationCollection? RowDecorations => _model.IsStatic ? TextDecorations.Underline : null;

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
    /// Enters inline editing. A freshly-added member starts blank for clean typing; an existing
    /// member is seeded with its formatted signature so the user can amend it.
    /// </summary>
    public void BeginEdit()
    {
        RawText = IsNewlyAdded ? string.Empty : MemberSignature.Format(_model);
        _editSeed = RawText;
        IsEditing = true;
    }

    /// <summary>
    /// Parses the edited text back into the model and leaves edit mode. The member's kind is
    /// preserved (a row never jumps compartments from typing); parsed values are mapped onto it.
    /// Structurally invalid text is rejected: the edit is discarded and the model keeps its previous
    /// value (the row re-renders its last-good signature). Undo is captured only for a real change to
    /// an already-established member — a newly-added member was already snapshotted when it was inserted.
    /// </summary>
    public void CommitEdit()
    {
        bool changed = !string.Equals(RawText, _editSeed, StringComparison.Ordinal);
        if (changed && MemberSignature.TryParse(RawText, ParseContext, out ClassMember? parsed, out _))
        {
            bool capture = !IsNewlyAdded;
            if (capture)
            {
                _context.BeginMemberEdit();
            }

            Apply(parsed);
            _context.EndMemberEdit();
        }

        // A member is "established" (no longer disposable) once it has a name.
        if (!string.IsNullOrWhiteSpace(_model.Name))
        {
            IsNewlyAdded = false;
        }

        IsEditing = false;
        RaiseAll();
    }

    public void CancelEdit() => IsEditing = false;

    private MemberKind ParseContext =>
        _model.Kind == MemberKind.EnumLiteral ? MemberKind.EnumLiteral : MemberKind.Field;

    private void Apply(ClassMember parsed)
    {
        _model.Visibility = parsed.Visibility;
        _model.Name = parsed.Name;
        switch (_model.Kind)
        {
            case MemberKind.EnumLiteral:
                break; // name only
            case MemberKind.Operation:
                _model.Parameters = parsed.Parameters ?? string.Empty;
                _model.Type = parsed.Type;
                break;
            default: // Field
                _model.Type = parsed.Type;
                _model.Parameters = null;
                break;
        }
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
        OnPropertyChanged(nameof(Visibility));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Type));
        OnPropertyChanged(nameof(Parameters));
        OnPropertyChanged(nameof(IsStatic));
        OnPropertyChanged(nameof(IsAbstract));
        OnPropertyChanged(nameof(IsOperation));
        OnPropertyChanged(nameof(DisplayText));
        OnPropertyChanged(nameof(RowFontStyle));
        OnPropertyChanged(nameof(RowDecorations));
    }
}
