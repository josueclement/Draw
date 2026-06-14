using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Input;
using Avalonia.Threading;
using Draw.App.ViewModels;

namespace Draw.App.Views.Interaction;

/// <summary>The owning node's row collection, abstracted so one <see cref="InlineRowEditController{TRow}"/>
/// drives both class members and entity columns. Implementations are thin wrappers over a node view model.</summary>
internal interface IInlineRowOwner<TRow>
    where TRow : class
{
    (ObservableCollection<TRow> List, int Index) Locate(TRow row);

    /// <summary>Inserts a new row of <paramref name="row"/>'s kind at <paramref name="index"/>
    /// (-1 = append) and returns it.</summary>
    TRow InsertLike(TRow row, int index);

    void DiscardEmptyNewRows();

    bool IsEditingAnyRow();

    void MoveRow(TRow row, int delta);
}

/// <summary>
/// Drives inline row editing (double-tap to edit, commit-on-lost-focus with blank discard, Enter-adds-next,
/// Tab/Shift+Tab navigation, Alt+Up/Down reorder) for any <see cref="IEditableRow"/>. The same logic
/// previously lived twice in the view code-behind (class members and entity columns); the row-type and
/// owner specifics now arrive via the owner adapter and the focus callbacks, so the flow can't drift.
/// Lives in <c>Views/Interaction/</c> because it performs the Avalonia focus dance (not a view model).
/// </summary>
internal sealed class InlineRowEditController<TRow>
    where TRow : class, IEditableRow
{
    private readonly Func<TRow, IInlineRowOwner<TRow>?> _ownerOf;
    private readonly Action<TRow, bool> _focusEditor;       // (row, selectAll) → focus its inline TextBox
    private readonly Action _returnFocusToCanvas;

    public InlineRowEditController(
        Func<TRow, IInlineRowOwner<TRow>?> ownerOf,
        Action<TRow, bool> focusEditor,
        Action returnFocusToCanvas)
    {
        _ownerOf = ownerOf;
        _focusEditor = focusEditor;
        _returnFocusToCanvas = returnFocusToCanvas;
    }

    // Double-tap: enter editing (undo is captured lazily on commit, only if the text changed) and focus
    // the editor with its text selected.
    public void BeginEdit(TRow row)
    {
        row.BeginEdit();
        _focusEditor(row, true); // select the existing text
    }

    // Lost focus: commit, then after focus settles drop an abandoned blank — unless focus moved to another
    // editor of the same owner (the Enter-adds-next / Tab flow keeps the session alive).
    public void CommitOnLostFocus(TRow row)
    {
        if (row.IsEditing)
        {
            row.CommitEdit();
        }

        if (_ownerOf(row) is { } owner)
        {
            Dispatcher.UIThread.Post(
                () =>
                {
                    if (!owner.IsEditingAnyRow())
                    {
                        owner.DiscardEmptyNewRows();
                    }
                },
                DispatcherPriority.Background);
        }
    }

    /// <summary>Handles an inline-editor key; returns true when the caller should mark the event handled.</summary>
    public bool HandleKey(TRow row, Key key, KeyModifiers modifiers)
    {
        if (_ownerOf(row) is not { } owner)
        {
            return false;
        }

        switch (key)
        {
            case Key.Enter:
                CommitAndAddNext(owner, row);
                return true;
            case Key.Tab:
                Navigate(owner, row, modifiers.HasFlag(KeyModifiers.Shift) ? -1 : +1);
                return true;
            case Key.Up when modifiers.HasFlag(KeyModifiers.Alt):
                owner.MoveRow(row, -1);
                _focusEditor(row, false); // keep the moved text, caret at end
                return true;
            case Key.Down when modifiers.HasFlag(KeyModifiers.Alt):
                owner.MoveRow(row, +1);
                _focusEditor(row, false); // keep the moved text, caret at end
                return true;
            default:
                return false;
        }
    }

    private void CommitAndAddNext(IInlineRowOwner<TRow> owner, TRow row)
    {
        row.CommitEdit();
        if (string.IsNullOrWhiteSpace(row.EditableName))
        {
            // Enter on an empty row finishes entry rather than spawning another blank.
            owner.DiscardEmptyNewRows();
            _returnFocusToCanvas();
            return;
        }

        (_, int index) = owner.Locate(row);
        AddAndEdit(owner, row, index + 1);
    }

    private void Navigate(IInlineRowOwner<TRow> owner, TRow row, int delta)
    {
        row.CommitEdit();
        if (string.IsNullOrWhiteSpace(row.EditableName))
        {
            owner.DiscardEmptyNewRows();
            _returnFocusToCanvas();
            return;
        }

        (ObservableCollection<TRow> list, int index) = owner.Locate(row);
        int next = index + delta;
        if (index < 0 || next < 0)
        {
            _returnFocusToCanvas();
            return;
        }

        if (next >= list.Count)
        {
            // Past the end → behave like Enter and add a fresh row of the same kind.
            AddAndEdit(owner, row, index: -1);
            return;
        }

        TRow target = list[next];
        target.BeginEdit();
        _focusEditor(target, true);
    }

    private void AddAndEdit(IInlineRowOwner<TRow> owner, TRow likeRow, int index)
    {
        TRow row = owner.InsertLike(likeRow, index);
        _focusEditor(row, false); // fresh row, caret ready for typing
    }
}

/// <summary>Class-member owner adapter over a <see cref="ClassNodeViewModel"/>.</summary>
internal sealed class ClassMemberRowOwner : IInlineRowOwner<ClassMemberViewModel>
{
    private readonly ClassNodeViewModel _node;

    public ClassMemberRowOwner(ClassNodeViewModel node) => _node = node;

    public (ObservableCollection<ClassMemberViewModel> List, int Index) Locate(ClassMemberViewModel row) => _node.Locate(row);

    public ClassMemberViewModel InsertLike(ClassMemberViewModel row, int index) => _node.InsertNewMember(row.Model.Kind, index);

    public void DiscardEmptyNewRows() => _node.DiscardEmptyNewMembers();

    public bool IsEditingAnyRow() => _node.PrimaryMembers.Concat(_node.Operations).Any(m => m.IsEditing);

    public void MoveRow(ClassMemberViewModel row, int delta) => _node.MoveMember(row, delta);
}

/// <summary>Entity-column owner adapter over an <see cref="EntityNodeViewModel"/>.</summary>
internal sealed class EntityColumnRowOwner : IInlineRowOwner<EntityColumnViewModel>
{
    private readonly EntityNodeViewModel _node;

    public EntityColumnRowOwner(EntityNodeViewModel node) => _node = node;

    public (ObservableCollection<EntityColumnViewModel> List, int Index) Locate(EntityColumnViewModel row) => _node.Locate(row);

    public EntityColumnViewModel InsertLike(EntityColumnViewModel row, int index) => _node.InsertNewColumn(index);

    public void DiscardEmptyNewRows() => _node.DiscardEmptyNewColumns();

    public bool IsEditingAnyRow() => _node.Columns.Any(c => c.IsEditing);

    public void MoveRow(EntityColumnViewModel row, int delta) => _node.MoveColumn(row, delta);
}
