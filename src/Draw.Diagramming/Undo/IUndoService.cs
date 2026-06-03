using System;
using Draw.Model.Documents;

namespace Draw.Diagramming.Undo;

/// <summary>
/// Memento-style undo/redo over whole-document snapshots. Callers <see cref="Capture"/>
/// the current state immediately before a mutating operation; <see cref="Undo"/> and
/// <see cref="Redo"/> return the document state to make live.
/// </summary>
public interface IUndoService
{
    bool CanUndo { get; }

    bool CanRedo { get; }

    /// <summary>Raised whenever <see cref="CanUndo"/>/<see cref="CanRedo"/> may have changed.</summary>
    event EventHandler? StateChanged;

    /// <summary>Snapshots <paramref name="current"/> before a mutation and clears the redo stack.</summary>
    void Capture(DiagramDocument current);

    /// <summary>Restores the previous snapshot; returns <paramref name="current"/> unchanged if none exists.</summary>
    DiagramDocument Undo(DiagramDocument current);

    /// <summary>Re-applies the next snapshot; returns <paramref name="current"/> unchanged if none exists.</summary>
    DiagramDocument Redo(DiagramDocument current);

    /// <summary>Clears all history (e.g. on new/open document).</summary>
    void Reset();
}
