using Draw.App.Configuration;
using Draw.App.ViewModels;
using Draw.Diagramming.Routing;
using Draw.Diagramming.Undo;
using Draw.Model.Documents;
using Draw.Model.Nodes;
using Draw.Model.Primitives;
using Draw.Model.Serialization;
using Draw.Model.Styling;
using Xunit;

namespace Draw.App.Tests;

public class InspectorViewModelTests
{
    private static DiagramDocumentViewModel CreateDocumentWithSelection(out ShapeNodeViewModel node)
    {
        DiagramDocumentViewModel doc = new(
            DiagramDocument.CreateEmpty(DiagramType.Freeform),
            new MementoUndoService(new JsonDocumentSerializer(), new UndoOptions()),
            new ConnectorRouter(new IConnectorRouteStrategy[] { new StraightRouter() }),
            new JsonDocumentSerializer(),
            new EditorOptions { SnapToGrid = false },
            filePath: null);
        node = doc.AddShape(ShapeKind.Rectangle, new Point2D(100, 100));
        return doc;
    }

    [Fact]
    public void SetTarget_LoadsValuesFromSelection()
    {
        DiagramDocumentViewModel doc = CreateDocumentWithSelection(out ShapeNodeViewModel node);
        node.Model.Style.Fill = ArgbColor.FromRgb(0x10, 0x20, 0x30);
        node.Model.Text = "Hello";

        InspectorViewModel inspector = new();
        inspector.SetTarget(doc);

        Assert.True(inspector.IsShapeSelected);
        Assert.Equal("Hello", inspector.Text);
        Assert.Equal("#FF102030", inspector.FillHex);
    }

    [Fact]
    public void SettingFillHex_AppliesToSelectionAndCapturesUndo()
    {
        DiagramDocumentViewModel doc = CreateDocumentWithSelection(out ShapeNodeViewModel node);
        InspectorViewModel inspector = new();
        inspector.SetTarget(doc);

        inspector.FillHex = "#FF112233";

        Assert.Equal(ArgbColor.FromRgb(0x11, 0x22, 0x33), node.Model.Style.Fill);
        Assert.True(doc.CanUndo);
    }

    [Fact]
    public void SettingBold_UpdatesSelectedNodeFont()
    {
        DiagramDocumentViewModel doc = CreateDocumentWithSelection(out ShapeNodeViewModel node);
        InspectorViewModel inspector = new();
        inspector.SetTarget(doc);

        inspector.Bold = true;

        Assert.True(node.Model.Style.Font.Bold);
    }

    [Fact]
    public void NoSelection_ReportsNoSelection()
    {
        InspectorViewModel inspector = new();

        inspector.SetTarget(target: null);

        Assert.True(inspector.HasNoSelection);
    }

    private static DiagramDocumentViewModel CreateDocumentWithClass(out ClassNodeViewModel node)
    {
        DiagramDocumentViewModel doc = new(
            DiagramDocument.CreateEmpty(DiagramType.Class),
            new MementoUndoService(new JsonDocumentSerializer(), new UndoOptions()),
            new ConnectorRouter(new IConnectorRouteStrategy[] { new StraightRouter() }),
            new JsonDocumentSerializer(),
            new EditorOptions { SnapToGrid = false },
            filePath: null);
        node = doc.AddClassNode(ClassNodeKind.Class, new Point2D(100, 100));
        return doc;
    }

    [Fact]
    public void SelectingClassNode_ReportsClassMode_AndLoadsSuggestions()
    {
        DiagramDocumentViewModel doc = CreateDocumentWithClass(out ClassNodeViewModel node);
        InspectorViewModel inspector = new();
        inspector.SetTarget(doc);

        Assert.True(inspector.IsClassNodeSelected);
        Assert.False(inspector.IsShapeSelected);
        Assert.Same(node, inspector.SelectedClassNode);
        Assert.Contains("string", inspector.TypeSuggestions);
    }

    [Fact]
    public void AddPrimaryMemberCommand_AddsMemberToSelectedClass()
    {
        DiagramDocumentViewModel doc = CreateDocumentWithClass(out ClassNodeViewModel node);
        InspectorViewModel inspector = new();
        inspector.SetTarget(doc);

        inspector.AddPrimaryMemberCommand.Execute(null);

        Assert.Single(node.PrimaryMembers);
    }

    [Fact]
    public void FillHex_AppliesToSelectedClassNode()
    {
        DiagramDocumentViewModel doc = CreateDocumentWithClass(out ClassNodeViewModel node);
        InspectorViewModel inspector = new();
        inspector.SetTarget(doc);

        inspector.FillHex = "#FF112233";

        Assert.Equal(ArgbColor.FromRgb(0x11, 0x22, 0x33), node.Model.Style.Fill);
    }

    [Fact]
    public void SelectingActor_ReportsLabelNode_AndLoadsName()
    {
        DiagramDocumentViewModel doc = new(
            DiagramDocument.CreateEmpty(DiagramType.UseCase),
            new MementoUndoService(new JsonDocumentSerializer(), new UndoOptions()),
            new ConnectorRouter(new IConnectorRouteStrategy[] { new StraightRouter() }),
            new JsonDocumentSerializer(),
            new EditorOptions { SnapToGrid = false },
            filePath: null);
        ActorNodeViewModel actor = (ActorNodeViewModel)doc.AddUseCaseNode(UseCaseNodeKind.Actor, new Point2D(100, 100));
        actor.Name = "Customer";

        InspectorViewModel inspector = new();
        inspector.SetTarget(doc);

        Assert.True(inspector.IsLabelNodeSelected);
        Assert.True(inspector.IsNodeSelected);     // shared style applies
        Assert.False(inspector.IsShapeSelected);
        Assert.False(inspector.HasNoSelection);
        Assert.Equal("Customer", inspector.Text);
    }

    [Fact]
    public void SettingText_AppliesToSelectedUseCaseNode()
    {
        DiagramDocumentViewModel doc = new(
            DiagramDocument.CreateEmpty(DiagramType.UseCase),
            new MementoUndoService(new JsonDocumentSerializer(), new UndoOptions()),
            new ConnectorRouter(new IConnectorRouteStrategy[] { new StraightRouter() }),
            new JsonDocumentSerializer(),
            new EditorOptions { SnapToGrid = false },
            filePath: null);
        UseCaseNodeViewModel useCase = (UseCaseNodeViewModel)doc.AddUseCaseNode(UseCaseNodeKind.UseCase, new Point2D(100, 100));

        InspectorViewModel inspector = new();
        inspector.SetTarget(doc);
        inspector.Text = "Place order";

        Assert.Equal("Place order", useCase.Model.Text);
    }
}
