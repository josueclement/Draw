using System.Linq;
using Jcl.Draw.App.Configuration;
using Jcl.Draw.App.ViewModels;
using Jcl.Draw.Diagramming.Routing;
using Jcl.Draw.Diagramming.Undo;
using Jcl.Draw.Model.Connectors;
using Jcl.Draw.Model.Documents;
using Jcl.Draw.Model.Nodes;
using Jcl.Draw.Model.Primitives;
using Jcl.Draw.Model.Serialization;
using Xunit;

namespace Jcl.Draw.App.Tests;

public class DiagramDocumentViewModelTests
{
    private static IConnectorRouter Router()
        => new ConnectorRouter(new IConnectorRouteStrategy[] { new StraightRouter(), new OrthogonalRouter(), new BezierRouter() });

    private static DiagramDocumentViewModel CreateDocument(bool snap = false)
        => new(
            DiagramDocument.CreateEmpty(DiagramType.Freeform),
            new MementoUndoService(new JsonDocumentSerializer(), new UndoOptions()),
            Router(),
            new JsonDocumentSerializer(),
            new EditorOptions { SnapToGrid = snap, GridSize = 10, DefaultShapeWidth = 120, DefaultShapeHeight = 70 },
            filePath: null);

    [Fact]
    public void AddShape_AddsCenteredSelectedNode_AndMarksModified()
    {
        DiagramDocumentViewModel doc = CreateDocument();

        ShapeNodeViewModel node = doc.AddShape(ShapeKind.Rectangle, new Point2D(100, 100));

        Assert.Same(node, Assert.Single(doc.Nodes));
        Assert.True(node.IsSelected);
        Assert.True(doc.HasSelection);
        Assert.True(doc.IsModified);
        Assert.Equal(40, node.X);
        Assert.Equal(65, node.Y);
    }

    [Fact]
    public void DeleteSelected_RemovesSelectedNodes()
    {
        DiagramDocumentViewModel doc = CreateDocument();
        doc.AddShape(ShapeKind.Rectangle, new Point2D(50, 50));
        doc.AddShape(ShapeKind.Ellipse, new Point2D(300, 300));
        doc.SelectInRect(new Rect2D(0, 0, 1000, 1000), additive: false);

        doc.DeleteSelected();

        Assert.Empty(doc.Nodes);
        Assert.False(doc.HasSelection);
    }

    [Fact]
    public void MoveSelectedBy_OffsetsSelectedNodes()
    {
        DiagramDocumentViewModel doc = CreateDocument();
        ShapeNodeViewModel node = doc.AddShape(ShapeKind.Rectangle, new Point2D(100, 100));

        doc.MoveSelectedBy(15, -25);

        Assert.Equal(55, node.X);
        Assert.Equal(40, node.Y);
    }

    [Fact]
    public void Undo_RemovesAddedShape_RedoRestoresIt()
    {
        DiagramDocumentViewModel doc = CreateDocument();
        doc.AddShape(ShapeKind.Rectangle, new Point2D(100, 100));
        Assert.Single(doc.Nodes);
        Assert.True(doc.CanUndo);

        doc.Undo();
        Assert.Empty(doc.Nodes);
        Assert.True(doc.CanRedo);

        doc.Redo();
        Assert.Single(doc.Nodes);
    }

    [Fact]
    public void SelectInRect_SelectsOnlyIntersectingNodes()
    {
        DiagramDocumentViewModel doc = CreateDocument();
        ShapeNodeViewModel near = doc.AddShape(ShapeKind.Rectangle, new Point2D(100, 100));
        ShapeNodeViewModel far = doc.AddShape(ShapeKind.Rectangle, new Point2D(900, 900));

        doc.SelectInRect(new Rect2D(0, 0, 300, 300), additive: false);

        Assert.True(near.IsSelected);
        Assert.False(far.IsSelected);
    }

    [Fact]
    public void SnapSelectionToGrid_AlignsPositionsWhenEnabled()
    {
        DiagramDocumentViewModel doc = CreateDocument(snap: true);
        ShapeNodeViewModel node = doc.AddShape(ShapeKind.Rectangle, new Point2D(100, 100));
        doc.MoveSelectedBy(3, 4);

        doc.SnapSelectionToGrid();

        Assert.Equal(0, node.X % 10, 3);
        Assert.Equal(0, node.Y % 10, 3);
    }

    [Fact]
    public void AddConnector_AddsAndSelectsConnector()
    {
        DiagramDocumentViewModel doc = CreateDocument();
        ShapeNodeViewModel a = doc.AddShape(ShapeKind.Rectangle, new Point2D(100, 100));
        ShapeNodeViewModel b = doc.AddShape(ShapeKind.Rectangle, new Point2D(400, 100));

        ConnectorViewModel? connector = doc.AddConnector(a.Id, b.Id, RelationshipKind.Composition);

        Assert.NotNull(connector);
        Assert.Same(connector, Assert.Single(doc.Connectors));
        Assert.True(doc.HasConnectorSelection);
        Assert.True(connector!.IsSelected);
    }

    [Fact]
    public void AddConnector_RejectsSelfLink()
    {
        DiagramDocumentViewModel doc = CreateDocument();
        ShapeNodeViewModel a = doc.AddShape(ShapeKind.Rectangle, new Point2D(100, 100));

        ConnectorViewModel? connector = doc.AddConnector(a.Id, a.Id, RelationshipKind.Association);

        Assert.Null(connector);
        Assert.Empty(doc.Connectors);
    }

    [Fact]
    public void UndoBackToSavedState_ClearsModifiedFlag()
    {
        DiagramDocumentViewModel doc = CreateDocument();
        doc.MarkSaved("/tmp/diagram.jcld");
        doc.AddShape(ShapeKind.Rectangle, new Point2D(100, 100));
        Assert.True(doc.IsModified);

        doc.Undo();

        Assert.False(doc.IsModified);
    }

    [Fact]
    public void DeletingNode_RemovesAttachedConnectors()
    {
        DiagramDocumentViewModel doc = CreateDocument();
        ShapeNodeViewModel a = doc.AddShape(ShapeKind.Rectangle, new Point2D(100, 100));
        ShapeNodeViewModel b = doc.AddShape(ShapeKind.Rectangle, new Point2D(400, 100));
        doc.AddConnector(a.Id, b.Id, RelationshipKind.Association);

        doc.SelectOnly(a);
        doc.DeleteSelected();

        Assert.Empty(doc.Connectors);
        Assert.Single(doc.Nodes);
    }

    [Fact]
    public void GetTypeSuggestions_OnEmptyDocument_ReturnsDistinctPrimitives()
    {
        DiagramDocumentViewModel doc = CreateDocument();

        var suggestions = doc.GetTypeSuggestions();

        Assert.Contains("string", suggestions);
        Assert.Contains("int", suggestions);
        Assert.Equal(suggestions.Count, suggestions.Distinct().Count());
    }
}
