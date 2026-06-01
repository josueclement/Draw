using System;
using System.Collections.Generic;
using Avalonia.Media;
using Jcl.Draw.Diagramming.Uml;
using Jcl.Draw.Model.Nodes;

namespace Jcl.Draw.App.ViewModels;

/// <summary>Bindable wrapper over a <see cref="ClassMember"/>, editable inline (raw text) or field-by-field.</summary>
public sealed class ClassMemberViewModel : ViewModelBase
{
    private readonly ClassMember _model;
    private readonly INodeEditContext _context;

    public ClassMemberViewModel(ClassMember model, INodeEditContext context)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public ClassMember Model => _model;

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

    /// <summary>Enters inline editing, seeding the editable text from the current model.</summary>
    public void BeginEdit()
    {
        RawText = MemberSignature.Format(_model);
        IsEditing = true;
    }

    /// <summary>Parses the edited text back into the model and leaves edit mode.</summary>
    public void CommitEdit()
    {
        MemberKind context = _model.Kind == MemberKind.EnumLiteral ? MemberKind.EnumLiteral : MemberKind.Field;
        ClassMember parsed = MemberSignature.Parse(RawText, context);
        _model.Visibility = parsed.Visibility;
        _model.Name = parsed.Name;
        _model.Type = parsed.Type;
        _model.Parameters = parsed.Parameters;
        _model.Kind = parsed.Kind;
        IsEditing = false;
        RaiseAll();
    }

    public void CancelEdit() => IsEditing = false;

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
