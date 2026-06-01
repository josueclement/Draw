using System.Linq;
using Jcl.Draw.App.ViewModels;
using Jcl.Draw.Model.Nodes;
using Xunit;

namespace Jcl.Draw.App.Tests;

public class ToolboxViewModelTests
{
    [Fact]
    public void HasClassNodeTools_ForEachKind()
    {
        ToolboxViewModel toolbox = new();
        Assert.Contains(toolbox.ClassNodes, t => t.Kind == ClassNodeKind.Class);
        Assert.Contains(toolbox.ClassNodes, t => t.Kind == ClassNodeKind.Interface);
        Assert.Contains(toolbox.ClassNodes, t => t.Kind == ClassNodeKind.Enum);
    }

    [Fact]
    public void SelectingClassNode_ClearsShapeAndConnector_AndSetsMode()
    {
        ToolboxViewModel toolbox = new();
        toolbox.SelectedShape = toolbox.Shapes.First();

        toolbox.SelectedClassNode = toolbox.ClassNodes.First();

        Assert.Null(toolbox.SelectedShape);
        Assert.Null(toolbox.SelectedConnector);
        Assert.True(toolbox.IsClassNodeMode);
        Assert.False(toolbox.IsSelectTool);
    }

    [Fact]
    public void SelectingShape_ClearsClassNode()
    {
        ToolboxViewModel toolbox = new();
        toolbox.SelectedClassNode = toolbox.ClassNodes.First();

        toolbox.SelectedShape = toolbox.Shapes.First();

        Assert.Null(toolbox.SelectedClassNode);
        Assert.False(toolbox.IsClassNodeMode);
    }
}
