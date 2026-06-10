using System;
using Avalonia.Media;
using Draw.Diagramming.Er;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>Bindable wrapper over an <see cref="EntityColumn"/>, editable inline (raw text) or field-by-field.</summary>
public sealed class EntityColumnViewModel : EditableItemViewModelBase<EntityColumn>
{
    public EntityColumnViewModel(EntityColumn model, INodeEditContext context)
        : base(model, context)
    {
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

    public bool IsPrimaryKey
    {
        get => Model.IsPrimaryKey;
        // A primary key is implicitly NOT NULL, so toggling it on also clears nullability.
        set => Edit(
            () =>
            {
                Model.IsPrimaryKey = value;
                if (value)
                {
                    Model.IsNullable = false;
                }
            },
            Model.IsPrimaryKey != value);
    }

    public bool IsForeignKey
    {
        get => Model.IsForeignKey;
        set => Edit(() => Model.IsForeignKey = value, Model.IsForeignKey != value);
    }

    public bool IsNullable
    {
        get => Model.IsNullable;
        set => Edit(() => Model.IsNullable = value, Model.IsNullable != value);
    }

    public bool IsUnique
    {
        get => Model.IsUnique;
        set => Edit(() => Model.IsUnique = value, Model.IsUnique != value);
    }

    public string DisplayText => ColumnSignature.Format(Model);

    /// <summary>A primary key reads as bold; the underline reinforces it as the key row.</summary>
    public FontWeight RowFontWeight => Model.IsPrimaryKey ? FontWeight.Bold : FontWeight.Normal;

    public TextDecorationCollection? RowDecorations => Model.IsPrimaryKey ? TextDecorations.Underline : null;

    protected override string ModelName => Model.Name;

    protected override string FormatModel() => ColumnSignature.Format(Model);

    protected override bool TryParseModel(string text, out EntityColumn? parsed) =>
        ColumnSignature.TryParse(text, out parsed, out _);

    protected override void Apply(EntityColumn parsed)
    {
        Model.Name = parsed.Name;
        Model.Type = parsed.Type;
        Model.IsPrimaryKey = parsed.IsPrimaryKey;
        Model.IsForeignKey = parsed.IsForeignKey;
        Model.IsUnique = parsed.IsUnique;
        Model.IsNullable = parsed.IsNullable;
    }

    protected override void RaiseAll()
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
