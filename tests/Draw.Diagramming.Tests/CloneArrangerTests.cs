using System;
using System.Linq;
using Draw.Diagramming.Layout;
using Draw.Model.Connectors;
using Draw.Model.Nodes;
using Draw.Model.Primitives;
using Xunit;

namespace Draw.Diagramming.Tests;

public class CloneArrangerTests
{
    private static ShapeNode Shape(string text, int z, double x = 0, double y = 0) =>
        new() { Text = text, ZIndex = z, Bounds = new Rect2D(x, y, 10, 10) };

    [Fact]
    public void Clone_AssignsFreshIds_AndRemapsConnectorEndpoints()
    {
        ShapeNode a = Shape("A", 1);
        ShapeNode b = Shape("B", 2, 100);
        Connector c = new() { SourceNodeId = a.Id, TargetNodeId = b.Id };

        CloneArranger.ClonedGraph g = CloneArranger.Clone(
            new NodeBase[] { a, b }, new[] { c }, Array.Empty<NodeBase>(), new Point2D(5, 5), null);

        ShapeNode ca = g.Nodes.OfType<ShapeNode>().Single(s => s.Text == "A");
        ShapeNode cb = g.Nodes.OfType<ShapeNode>().Single(s => s.Text == "B");
        Assert.NotEqual(a.Id, ca.Id);
        Assert.NotEqual(b.Id, cb.Id);

        Connector cc = Assert.Single(g.Connectors);
        Assert.NotEqual(c.Id, cc.Id);
        Assert.Equal(ca.Id, cc.SourceNodeId); // endpoints remapped to the clones
        Assert.Equal(cb.Id, cc.TargetNodeId);
    }

    [Fact]
    public void Clone_TranslatesBounds_WhenNoGrid()
    {
        ShapeNode a = Shape("A", 0, 3, 4);
        CloneArranger.ClonedGraph g = CloneArranger.Clone(
            new NodeBase[] { a }, Array.Empty<Connector>(), Array.Empty<NodeBase>(), new Point2D(5, 7), null);

        NodeBase clone = Assert.Single(g.Nodes);
        Assert.Equal(8, clone.Bounds.X);
        Assert.Equal(11, clone.Bounds.Y);
    }

    [Fact]
    public void Clone_SnapsPosition_WhenGridGiven()
    {
        ShapeNode a = Shape("A", 0, 12, 12);
        CloneArranger.ClonedGraph g = CloneArranger.Clone(
            new NodeBase[] { a }, Array.Empty<Connector>(), Array.Empty<NodeBase>(), new Point2D(0, 0), 10);

        NodeBase clone = Assert.Single(g.Nodes);
        Assert.Equal(10, clone.Bounds.X);
        Assert.Equal(10, clone.Bounds.Y);
    }

    [Fact]
    public void Clone_OrdinaryClonesRiseAboveExistingMax_PreservingBatchOrder()
    {
        ShapeNode existing = Shape("E", 5);
        ShapeNode lower = Shape("low", 1);
        ShapeNode higher = Shape("high", 3);

        // Pass sources out of order to prove they are cloned in ascending ZIndex.
        CloneArranger.ClonedGraph g = CloneArranger.Clone(
            new NodeBase[] { higher, lower }, Array.Empty<Connector>(),
            new NodeBase[] { existing }, new Point2D(0, 0), null);

        Assert.Equal(6, g.Nodes.OfType<ShapeNode>().Single(s => s.Text == "low").ZIndex);
        Assert.Equal(7, g.Nodes.OfType<ShapeNode>().Single(s => s.Text == "high").ZIndex);
    }

    [Fact]
    public void Clone_BoundaryClonesSinkBelowExistingMin()
    {
        ShapeNode existing = Shape("E", 0);
        SystemBoundaryNode b1 = new() { Title = "b1", ZIndex = 4, Bounds = new Rect2D(0, 0, 50, 50) };
        SystemBoundaryNode b2 = new() { Title = "b2", ZIndex = 9, Bounds = new Rect2D(0, 0, 50, 50) };

        CloneArranger.ClonedGraph g = CloneArranger.Clone(
            new NodeBase[] { b1, b2 }, Array.Empty<Connector>(),
            new NodeBase[] { existing }, new Point2D(0, 0), null);

        // Existing min is 0; boundaries sink to -1 then -2 in ascending source order.
        Assert.Equal(-1, g.Nodes.OfType<SystemBoundaryNode>().Single(s => s.Title == "b1").ZIndex);
        Assert.Equal(-2, g.Nodes.OfType<SystemBoundaryNode>().Single(s => s.Title == "b2").ZIndex);
    }

    [Fact]
    public void Clone_EmptyExisting_OrdinaryStartsAtZero_BoundaryAtMinusOne()
    {
        ShapeNode ord = Shape("ord", 0);
        SystemBoundaryNode bnd = new() { Title = "bnd", ZIndex = 0, Bounds = new Rect2D(0, 0, 50, 50) };

        CloneArranger.ClonedGraph g = CloneArranger.Clone(
            new NodeBase[] { ord, bnd }, Array.Empty<Connector>(),
            Array.Empty<NodeBase>(), new Point2D(0, 0), null);

        Assert.Equal(0, g.Nodes.OfType<ShapeNode>().Single().ZIndex);
        Assert.Equal(-1, g.Nodes.OfType<SystemBoundaryNode>().Single().ZIndex);
    }

    [Fact]
    public void Clone_DropsConnectorWhoseEndpointIsUnknown()
    {
        ShapeNode a = Shape("A", 0);
        ShapeNode b = Shape("B", 1);
        Connector kept = new() { SourceNodeId = a.Id, TargetNodeId = b.Id };
        // Target is neither a cloned node nor an existing document node -> a true orphan.
        Connector dangling = new() { SourceNodeId = a.Id, TargetNodeId = Guid.NewGuid() };

        CloneArranger.ClonedGraph g = CloneArranger.Clone(
            new NodeBase[] { a, b }, new[] { kept, dangling },
            Array.Empty<NodeBase>(), new Point2D(0, 0), null);

        Assert.Single(g.Connectors); // the dangling connector is dropped
    }

    [Fact]
    public void Clone_KeepsBoundaryConnector_ToExistingNonClonedNode()
    {
        // Only A is duplicated; B stays put but still exists in the document. The connector to B
        // must survive, its duplicated end repointed at the clone and its loose end left on B.
        ShapeNode a = Shape("A", 0);
        ShapeNode b = Shape("B", 1, 100);
        Connector boundary = new() { SourceNodeId = a.Id, TargetNodeId = b.Id };

        CloneArranger.ClonedGraph g = CloneArranger.Clone(
            new NodeBase[] { a }, new[] { boundary },
            new NodeBase[] { a, b }, new Point2D(5, 5), null);

        ShapeNode ca = Assert.Single(g.Nodes.OfType<ShapeNode>());
        Assert.NotEqual(a.Id, ca.Id);

        Connector cc = Assert.Single(g.Connectors);
        Assert.NotEqual(boundary.Id, cc.Id);
        Assert.Equal(ca.Id, cc.SourceNodeId); // duplicated end -> the clone
        Assert.Equal(b.Id, cc.TargetNodeId);  // loose end -> original neighbour, unchanged
    }

    [Fact]
    public void Clone_KeepsBoundaryConnector_WhenExistingNodeIsTheSource()
    {
        // Same as above but the existing (non-cloned) node is the connector's source end.
        ShapeNode a = Shape("A", 0);
        ShapeNode b = Shape("B", 1, 100);
        Connector boundary = new() { SourceNodeId = b.Id, TargetNodeId = a.Id };

        CloneArranger.ClonedGraph g = CloneArranger.Clone(
            new NodeBase[] { a }, new[] { boundary },
            new NodeBase[] { a, b }, new Point2D(5, 5), null);

        ShapeNode ca = Assert.Single(g.Nodes.OfType<ShapeNode>());
        Connector cc = Assert.Single(g.Connectors);
        Assert.Equal(b.Id, cc.SourceNodeId);  // loose end -> original neighbour, unchanged
        Assert.Equal(ca.Id, cc.TargetNodeId); // duplicated end -> the clone
    }

    [Fact]
    public void Clone_DropsConnector_TouchingNoClone()
    {
        // A is duplicated; the connector links two other existing nodes that are not part of the
        // duplication, so it is not ours to clone.
        ShapeNode a = Shape("A", 0);
        ShapeNode b = Shape("B", 1, 100);
        ShapeNode c = Shape("C", 2, 200);
        Connector unrelated = new() { SourceNodeId = b.Id, TargetNodeId = c.Id };

        CloneArranger.ClonedGraph g = CloneArranger.Clone(
            new NodeBase[] { a }, new[] { unrelated },
            new NodeBase[] { a, b, c }, new Point2D(5, 5), null);

        Assert.Empty(g.Connectors);
    }

    [Fact]
    public void Clone_EmptySource_YieldsEmptyGraph()
    {
        CloneArranger.ClonedGraph g = CloneArranger.Clone(
            Array.Empty<NodeBase>(), Array.Empty<Connector>(),
            Array.Empty<NodeBase>(), new Point2D(1, 1), null);

        Assert.Empty(g.Nodes);
        Assert.Empty(g.Connectors);
    }
}
