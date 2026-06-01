using System;
using System.Collections.Generic;
using Jcl.Draw.Model.Documents;
using Jcl.Draw.Model.Serialization;

namespace Jcl.Draw.Diagramming.Undo;

/// <summary>
/// Snapshot-based undo. Each snapshot is a deep clone produced by the document
/// serializer, so the model needs no per-mutation command plumbing. History is
/// capped at <see cref="UndoOptions.MaxDepth"/>.
/// </summary>
public sealed class MementoUndoService : IUndoService
{
    private readonly IDocumentSerializer _serializer;
    private readonly int _maxDepth;
    private readonly List<DiagramDocument> _undo = new();
    private readonly List<DiagramDocument> _redo = new();

    public MementoUndoService(IDocumentSerializer serializer, UndoOptions options)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        ArgumentNullException.ThrowIfNull(options);
        _maxDepth = Math.Max(1, options.MaxDepth);
    }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public event EventHandler? StateChanged;

    public void Capture(DiagramDocument current)
    {
        ArgumentNullException.ThrowIfNull(current);

        _undo.Add(_serializer.Clone(current));
        if (_undo.Count > _maxDepth)
        {
            _undo.RemoveAt(0);
        }

        _redo.Clear();
        OnStateChanged();
    }

    public DiagramDocument Undo(DiagramDocument current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (_undo.Count == 0)
        {
            return current;
        }

        DiagramDocument previous = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(_serializer.Clone(current));
        OnStateChanged();
        return previous;
    }

    public DiagramDocument Redo(DiagramDocument current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (_redo.Count == 0)
        {
            return current;
        }

        DiagramDocument next = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(_serializer.Clone(current));
        OnStateChanged();
        return next;
    }

    public void Reset()
    {
        _undo.Clear();
        _redo.Clear();
        OnStateChanged();
    }

    private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
