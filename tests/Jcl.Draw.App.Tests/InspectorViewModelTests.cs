using Jcl.Draw.App.Configuration;
using Jcl.Draw.App.ViewModels;
using Jcl.Draw.Diagramming.Routing;
using Jcl.Draw.Diagramming.Undo;
using Jcl.Draw.Model.Documents;
using Jcl.Draw.Model.Nodes;
using Jcl.Draw.Model.Primitives;
using Jcl.Draw.Model.Serialization;
using Jcl.Draw.Model.Styling;
using Xunit;

namespace Jcl.Draw.App.Tests;

public class InspectorViewModelTests
{
    private static DiagramDocumentViewModel CreateDocumentWithSelection(out ShapeNodeViewModel node)
    {
        DiagramDocumentViewModel doc = new(
            DiagramDocument.CreateEmpty(DiagramType.Freeform),
            new MementoUndoService(new JsonDocumentSerializer(), new UndoOptions()),
            new ConnectorRouter(new IConnectorRouteStrategy[] { new StraightRouter() }),
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
}
