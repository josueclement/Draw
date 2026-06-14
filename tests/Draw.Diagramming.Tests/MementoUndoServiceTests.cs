using System;
using Draw.Diagramming.Undo;
using Draw.Model.Documents;
using Draw.Model.Serialization;
using Xunit;

namespace Draw.Diagramming.Tests;

public class MementoUndoServiceTests
{
    private static DiagramDocument Doc(string? title = null) => new() { Title = title };

    private static MementoUndoService Build(IDocumentSerializer serializer, int maxDepth = 100)
        => new(serializer, new UndoOptions { MaxDepth = maxDepth });

    /// <summary>A non-JSON serializer that records clone calls and copies only the identifying
    /// fields — lets the stacking/eviction tests assert behaviour without depending on full
    /// serialization fidelity, and proves <see cref="MementoUndoService.Capture"/> clones rather
    /// than stores by reference.</summary>
    private sealed class RecordingSerializer : IDocumentSerializer
    {
        public int CloneCount { get; private set; }

        public string Serialize(DiagramDocument document) => throw new NotSupportedException();

        public DiagramDocument Deserialize(string json) => throw new NotSupportedException();

        public DiagramDocument Clone(DiagramDocument document)
        {
            CloneCount++;
            return new DiagramDocument { Title = document.Title, DiagramType = document.DiagramType };
        }
    }

    [Fact]
    public void Ctor_NullSerializer_Throws()
        => Assert.Throws<ArgumentNullException>(() => new MementoUndoService(null!, new UndoOptions()));

    [Fact]
    public void Ctor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new MementoUndoService(new RecordingSerializer(), null!));

    [Fact]
    public void New_Service_CannotUndoOrRedo()
    {
        MementoUndoService svc = Build(new RecordingSerializer());
        Assert.False(svc.CanUndo);
        Assert.False(svc.CanRedo);
    }

    [Fact]
    public void Capture_StoresClone_NotReference()
    {
        RecordingSerializer serializer = new();
        MementoUndoService svc = Build(serializer);
        DiagramDocument live = Doc("live");

        svc.Capture(live);

        Assert.Equal(1, serializer.CloneCount);
        // Undoing returns the captured snapshot, which must be a distinct instance from the live doc.
        Assert.NotSame(live, svc.Undo(Doc("current")));
    }

    [Fact]
    public void Capture_EnablesUndo_AndRaisesStateChanged()
    {
        MementoUndoService svc = Build(new RecordingSerializer());
        int raised = 0;
        svc.StateChanged += (_, _) => raised++;

        svc.Capture(Doc());

        Assert.True(svc.CanUndo);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Undo_ReturnsPreviousSnapshot_AndMovesCurrentToRedo()
    {
        MementoUndoService svc = Build(new RecordingSerializer());
        svc.Capture(Doc("A"));            // snapshot of state A

        DiagramDocument restored = svc.Undo(Doc("B"));

        Assert.Equal("A", restored.Title);
        Assert.False(svc.CanUndo);
        Assert.True(svc.CanRedo);
    }

    [Fact]
    public void Redo_ReturnsTheStateUndoCaptured()
    {
        MementoUndoService svc = Build(new RecordingSerializer());
        svc.Capture(Doc("A"));
        svc.Undo(Doc("B"));               // pushes B onto redo, returns A

        DiagramDocument redone = svc.Redo(Doc("A"));

        Assert.Equal("B", redone.Title);
        Assert.True(svc.CanUndo);
        Assert.False(svc.CanRedo);
    }

    [Fact]
    public void Undo_IsLastInFirstOut()
    {
        MementoUndoService svc = Build(new RecordingSerializer());
        svc.Capture(Doc("s1"));
        svc.Capture(Doc("s2"));
        svc.Capture(Doc("s3"));

        Assert.Equal("s3", svc.Undo(Doc("live")).Title);
        Assert.Equal("s2", svc.Undo(Doc("s3")).Title);
        Assert.Equal("s1", svc.Undo(Doc("s2")).Title);
        Assert.False(svc.CanUndo);
    }

    [Fact]
    public void Capture_ClearsRedo()
    {
        MementoUndoService svc = Build(new RecordingSerializer());
        svc.Capture(Doc("A"));
        svc.Undo(Doc("B"));               // redo now has one entry
        Assert.True(svc.CanRedo);

        svc.Capture(Doc("C"));            // a fresh edit invalidates the redo branch

        Assert.False(svc.CanRedo);
    }

    [Fact]
    public void Undo_EmptyStack_ReturnsSameInstance_AndDoesNotRaise()
    {
        MementoUndoService svc = Build(new RecordingSerializer());
        int raised = 0;
        svc.StateChanged += (_, _) => raised++;
        DiagramDocument current = Doc("current");

        Assert.Same(current, svc.Undo(current));
        Assert.Equal(0, raised);
    }

    [Fact]
    public void Redo_EmptyStack_ReturnsSameInstance_AndDoesNotRaise()
    {
        MementoUndoService svc = Build(new RecordingSerializer());
        int raised = 0;
        svc.StateChanged += (_, _) => raised++;
        DiagramDocument current = Doc("current");

        Assert.Same(current, svc.Redo(current));
        Assert.Equal(0, raised);
    }

    [Fact]
    public void Capture_BeyondMaxDepth_EvictsOldest()
    {
        MementoUndoService svc = Build(new RecordingSerializer(), maxDepth: 2);
        svc.Capture(Doc("a"));
        svc.Capture(Doc("b"));
        svc.Capture(Doc("c"));            // "a" is evicted from the front

        Assert.Equal("c", svc.Undo(Doc("live")).Title);
        Assert.Equal("b", svc.Undo(Doc("c")).Title);
        Assert.False(svc.CanUndo);        // only 2 retained
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void MaxDepth_IsClampedToAtLeastOne(int configured)
    {
        MementoUndoService svc = Build(new RecordingSerializer(), maxDepth: configured);
        svc.Capture(Doc("a"));
        svc.Capture(Doc("b"));            // effective depth 1 → "a" evicted

        Assert.Equal("b", svc.Undo(Doc("live")).Title);
        Assert.False(svc.CanUndo);
    }

    [Fact]
    public void Reset_ClearsBothStacks_AndRaisesStateChanged()
    {
        MementoUndoService svc = Build(new RecordingSerializer());
        svc.Capture(Doc("A"));
        svc.Undo(Doc("B"));               // both stacks now populated
        int raised = 0;
        svc.StateChanged += (_, _) => raised++;

        svc.Reset();

        Assert.False(svc.CanUndo);
        Assert.False(svc.CanRedo);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Capture_NullDocument_Throws()
        => Assert.Throws<ArgumentNullException>(() => Build(new RecordingSerializer()).Capture(null!));

    [Fact]
    public void Undo_NullDocument_Throws()
        => Assert.Throws<ArgumentNullException>(() => Build(new RecordingSerializer()).Undo(null!));

    [Fact]
    public void Redo_NullDocument_Throws()
        => Assert.Throws<ArgumentNullException>(() => Build(new RecordingSerializer()).Redo(null!));

    [Fact]
    public void RealSerializer_Capture_ProducesIndependentSnapshot()
    {
        // Against the production JSON serializer, an undo snapshot must be a deep, independent copy.
        MementoUndoService svc = Build(new JsonDocumentSerializer());
        DiagramDocument live = Doc("original");
        svc.Capture(live);

        live.Title = "mutated after capture";
        DiagramDocument restored = svc.Undo(Doc("current"));

        Assert.Equal("original", restored.Title); // snapshot untouched by the later mutation
    }
}
