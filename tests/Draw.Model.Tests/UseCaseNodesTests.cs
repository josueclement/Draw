using System;
using Draw.Model.Documents;
using Draw.Model.Nodes;
using Draw.Model.Primitives;
using Draw.Model.Serialization;
using Xunit;

namespace Draw.Model.Tests;

public class UseCaseNodesTests
{
    [Fact]
    public void Clone_Actor_CopiesNameAndBase()
    {
        ActorNode node = new() { Id = Guid.NewGuid(), Name = "Customer", Bounds = new Rect2D(1, 2, 48, 84) };

        ActorNode clone = Assert.IsType<ActorNode>(node.Clone());
        clone.Name = "Admin";

        Assert.Equal(node.Id, clone.Id);
        Assert.Equal("Customer", node.Name);
        Assert.Equal("Admin", clone.Name);
        Assert.Equal(new Rect2D(1, 2, 48, 84), clone.Bounds);
    }

    [Fact]
    public void RoundTrip_PreservesAllThreeNodeKinds()
    {
        JsonDocumentSerializer serializer = new();
        DiagramDocument doc = new() { DiagramType = DiagramType.UseCase };
        doc.Nodes.Add(new ActorNode { Name = "Customer", Bounds = new Rect2D(0, 0, 48, 84) });
        doc.Nodes.Add(new UseCaseNode { Text = "Place order", Bounds = new Rect2D(80, 0, 130, 72) });
        doc.Nodes.Add(new SystemBoundaryNode { Title = "Shop", Bounds = new Rect2D(0, 0, 320, 220) });

        DiagramDocument back = serializer.Deserialize(serializer.Serialize(doc));

        ActorNode actor = Assert.IsType<ActorNode>(back.Nodes[0]);
        Assert.Equal("Customer", actor.Name);
        UseCaseNode useCase = Assert.IsType<UseCaseNode>(back.Nodes[1]);
        Assert.Equal("Place order", useCase.Text);
        SystemBoundaryNode boundary = Assert.IsType<SystemBoundaryNode>(back.Nodes[2]);
        Assert.Equal("Shop", boundary.Title);
    }
}
