namespace Draw.Diagramming.Undo;

/// <summary>Configuration for the memento undo/redo history.</summary>
public sealed class UndoOptions
{
    /// <summary>Maximum number of retained undo snapshots. Older snapshots are discarded.</summary>
    public int MaxDepth { get; set; } = 100;
}
