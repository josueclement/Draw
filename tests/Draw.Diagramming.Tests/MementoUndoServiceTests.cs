using Draw.Diagramming.Undo;
using Draw.Model.Documents;
using Draw.Model.Nodes;
using Draw.Model.Serialization;
using Xunit;

namespace Draw.Diagramming.Tests;

public class MementoUndoServiceTests
{
    private static MementoUndoService CreateService(int maxDepth = 100)
        => new(new JsonDocumentSerializer(), new UndoOptions { MaxDepth = maxDepth });

    private static DiagramDocument Doc(string title) => new() { Title = title };

    [Fact]
    public void FreshService_CannotUndoOrRedo()
    {
        MementoUndoService service = CreateService();

        Assert.False(service.CanUndo);
        Assert.False(service.CanRedo);
    }

    [Fact]
    public void Capture_EnablesUndo()
    {
        MementoUndoService service = CreateService();

        service.Capture(Doc("A"));

        Assert.True(service.CanUndo);
        Assert.False(service.CanRedo);
    }

    [Fact]
    public void UndoThenRedo_RestoresStates()
    {
        MementoUndoService service = CreateService();
        DiagramDocument live = Doc("A");

        service.Capture(live);
        live.Title = "B";

        DiagramDocument afterUndo = service.Undo(live);
        Assert.Equal("A", afterUndo.Title);
        Assert.True(service.CanRedo);

        DiagramDocument afterRedo = service.Redo(afterUndo);
        Assert.Equal("B", afterRedo.Title);
    }

    [Fact]
    public void Capture_ClearsRedoStack()
    {
        MementoUndoService service = CreateService();
        DiagramDocument live = Doc("A");
        service.Capture(live);
        live.Title = "B";
        DiagramDocument afterUndo = service.Undo(live);
        Assert.True(service.CanRedo);

        service.Capture(afterUndo);

        Assert.False(service.CanRedo);
    }

    [Fact]
    public void Capture_BeyondMaxDepth_DropsOldestSnapshots()
    {
        MementoUndoService service = CreateService(maxDepth: 2);
        DiagramDocument live = Doc("live");

        service.Capture(Doc("A"));
        service.Capture(Doc("B"));
        service.Capture(Doc("C"));

        Assert.Equal("C", service.Undo(live).Title);
        Assert.Equal("B", service.Undo(Doc("x")).Title);
        Assert.False(service.CanUndo);
    }

    [Fact]
    public void Snapshot_IsIndependentOfLaterLiveMutation()
    {
        MementoUndoService service = CreateService();
        DiagramDocument live = new();
        ShapeNode node = new() { Text = "original" };
        live.Nodes.Add(node);

        service.Capture(live);
        node.Text = "changed";
        live.Nodes.Add(new ShapeNode());

        DiagramDocument restored = service.Undo(live);

        ShapeNode restoredNode = Assert.IsType<ShapeNode>(Assert.Single(restored.Nodes));
        Assert.Equal("original", restoredNode.Text);
    }

    [Fact]
    public void StateChanged_RaisedOnCapture()
    {
        MementoUndoService service = CreateService();
        int count = 0;
        service.StateChanged += (_, _) => count++;

        service.Capture(Doc("A"));

        Assert.Equal(1, count);
    }

    [Fact]
    public void Reset_ClearsHistory()
    {
        MementoUndoService service = CreateService();
        service.Capture(Doc("A"));

        service.Reset();

        Assert.False(service.CanUndo);
        Assert.False(service.CanRedo);
    }

    [Fact]
    public void Undo_WithNoHistory_ReturnsCurrentUnchanged()
    {
        MementoUndoService service = CreateService();
        DiagramDocument live = Doc("A");

        DiagramDocument result = service.Undo(live);

        Assert.Same(live, result);
    }
}
