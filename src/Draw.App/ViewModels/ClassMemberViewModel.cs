using System;
using Avalonia.Media;
using Draw.Diagramming.Uml;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>Bindable wrapper over a <see cref="ClassMember"/>, editable inline (raw text) or field-by-field.</summary>
public sealed class ClassMemberViewModel : EditableItemViewModelBase<ClassMember>
{
    public ClassMemberViewModel(ClassMember model, INodeEditContext context)
        : base(model, context)
    {
    }

    public bool IsEnumLiteral => Model.Kind == MemberKind.EnumLiteral;

    public MemberVisibility Visibility
    {
        get => Model.Visibility;
        set => Edit(() => Model.Visibility = value, Model.Visibility != value);
    }

    public string Name
    {
        get => Model.Name;
        set => Edit(() => Model.Name = value ?? string.Empty, !string.Equals(Model.Name, value, StringComparison.Ordinal));
    }

    public string? Type
    {
        get => Model.Type;
        set => Edit(() => Model.Type = string.IsNullOrWhiteSpace(value) ? null : value, !string.Equals(Model.Type, value, StringComparison.Ordinal));
    }

    public string? Parameters
    {
        get => Model.Parameters;
        set => Edit(() => Model.Parameters = value, !string.Equals(Model.Parameters, value, StringComparison.Ordinal));
    }

    public bool IsStatic
    {
        get => Model.IsStatic;
        set => Edit(() => Model.IsStatic = value, Model.IsStatic != value);
    }

    public bool IsAbstract
    {
        get => Model.IsAbstract;
        set => Edit(() => Model.IsAbstract = value, Model.IsAbstract != value);
    }

    public bool IsOperation => Model.Kind == MemberKind.Operation;

    public string DisplayText => MemberSignature.Format(Model);

    public FontStyle RowFontStyle => Model.IsAbstract ? FontStyle.Italic : FontStyle.Normal;

    public TextDecorationCollection? RowDecorations => Model.IsStatic ? TextDecorations.Underline : null;

    protected override string ModelName => Model.Name;

    protected override string FormatModel() => MemberSignature.Format(Model);

    protected override bool TryParseModel(string text, out ClassMember? parsed) =>
        MemberSignature.TryParse(text, ParseContext, out parsed, out _);

    private MemberKind ParseContext =>
        Model.Kind == MemberKind.EnumLiteral ? MemberKind.EnumLiteral : MemberKind.Field;

    protected override void Apply(ClassMember parsed)
    {
        Model.Visibility = parsed.Visibility;
        Model.Name = parsed.Name;
        switch (Model.Kind)
        {
            case MemberKind.EnumLiteral:
                break; // name only
            case MemberKind.Operation:
                Model.Parameters = parsed.Parameters ?? string.Empty;
                Model.Type = parsed.Type;
                break;
            default: // Field
                Model.Type = parsed.Type;
                Model.Parameters = null;
                break;
        }
    }

    protected override void RaiseAll()
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
