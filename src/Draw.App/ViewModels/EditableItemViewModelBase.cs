using System;
using System.Collections.Generic;

namespace Draw.App.ViewModels;

/// <summary>
/// The inline-editing surface an <c>InlineRowEditController</c> drives, independent of the row's model
/// type, so one controller serves both class members and entity columns.
/// </summary>
public interface IEditableRow
{
    bool IsEditing { get; }

    /// <summary>The row's current name; blank means a not-yet-named row that can be discarded.</summary>
    string EditableName { get; }

    void BeginEdit();

    void CommitEdit();
}

/// <summary>
/// Shared lifecycle for a bindable wrapper over a model item editable inline (raw text) or
/// field-by-field. Subclasses (class members, entity columns) supply only the type-specific
/// format/parse/apply hooks; the edit/commit/cancel flow and its undo-capture contract live here so
/// the two cannot drift apart.
/// </summary>
public abstract class EditableItemViewModelBase<TModel> : ViewModelBase, IEditableRow
    where TModel : class
{
    private string _editSeed = string.Empty;

    protected EditableItemViewModelBase(TModel model, INodeEditContext context)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public TModel Model { get; }

    protected INodeEditContext Context { get; }

    /// <summary>True for an item created blank via the canvas add flow until it is first given a name.</summary>
    public bool IsNewlyAdded { get; set; }

    public IReadOnlyList<string> TypeSuggestions => Context.GetTypeSuggestions();

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
    /// Enters inline editing. A freshly-added item starts blank for clean typing; an existing item
    /// is seeded with its formatted signature so the user can amend it.
    /// </summary>
    public void BeginEdit()
    {
        RawText = IsNewlyAdded ? string.Empty : FormatModel();
        _editSeed = RawText;
        IsEditing = true;
    }

    /// <summary>
    /// Parses the edited text back into the model and leaves edit mode. Structurally invalid text is
    /// rejected: the edit is discarded and the model keeps its previous value (the row re-renders its
    /// last-good signature). Undo is captured only for a real change to an already-established
    /// item — a newly-added item was already snapshotted when it was inserted.
    /// </summary>
    public void CommitEdit()
    {
        bool changed = !string.Equals(RawText, _editSeed, StringComparison.Ordinal);
        if (changed && TryParseModel(RawText, out TModel? parsed))
        {
            bool capture = !IsNewlyAdded;
            if (capture)
            {
                Context.BeginMemberEdit();
            }

            Apply(parsed!);
            Context.EndMemberEdit();
        }

        // An item is "established" (no longer disposable) once it has a name.
        if (!string.IsNullOrWhiteSpace(ModelName))
        {
            IsNewlyAdded = false;
        }

        IsEditing = false;
        RaiseAll();
    }

    public void CancelEdit() => IsEditing = false;

    /// <summary>Applies a single field mutation under undo capture, then re-raises bindings.</summary>
    protected void Edit(Action mutate, bool changed)
    {
        if (!changed)
        {
            return;
        }

        Context.BeginMemberEdit();
        mutate();
        Context.EndMemberEdit();
        RaiseAll();
    }

    /// <summary>Formats the model into the inline-edit seed text.</summary>
    protected abstract string FormatModel();

    /// <summary>Parses inline-edit text into a model, returning false for structurally invalid input.</summary>
    protected abstract bool TryParseModel(string text, out TModel? parsed);

    /// <summary>Maps the parsed values onto the live model.</summary>
    protected abstract void Apply(TModel parsed);

    /// <summary>The model's current name, used to decide when a newly-added item becomes established.</summary>
    protected abstract string ModelName { get; }

    /// <summary><see cref="IEditableRow.EditableName"/>: the row's name for the controller's blank check.</summary>
    public string EditableName => ModelName;

    /// <summary>Re-raises every derived property bound to the row.</summary>
    protected abstract void RaiseAll();
}
