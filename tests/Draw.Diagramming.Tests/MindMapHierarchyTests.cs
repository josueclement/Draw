using System;
using System.Collections.Generic;
using Draw.Diagramming.MindMap;
using Draw.Model.Connectors;
using Xunit;

namespace Draw.Diagramming.Tests;

public class MindMapHierarchyTests
{
    private static Connector Branch(Guid source, Guid target)
        => new() { SourceNodeId = source, TargetNodeId = target, IsMindMapBranch = true };

    private static Connector Plain(Guid source, Guid target)
        => new() { SourceNodeId = source, TargetNodeId = target, IsMindMapBranch = false };

    [Fact]
    public void NoConnectors_IsEmpty()
        => Assert.Empty(MindMapHierarchy.ComputeDepths(Array.Empty<Connector>()));

    [Fact]
    public void OnlyNonBranchConnectors_AreIgnored()
    {
        Guid a = Guid.NewGuid();
        Guid b = Guid.NewGuid();
        Assert.Empty(MindMapHierarchy.ComputeDepths(new[] { Plain(a, b) }));
    }

    [Fact]
    public void LinearChain_AssignsIncreasingDepths()
    {
        Guid root = Guid.NewGuid();
        Guid a = Guid.NewGuid();
        Guid b = Guid.NewGuid();
        Guid c = Guid.NewGuid();

        IReadOnlyDictionary<Guid, int> depths = MindMapHierarchy.ComputeDepths(
            new[] { Branch(root, a), Branch(a, b), Branch(b, c) });

        Assert.Equal(0, depths[root]);
        Assert.Equal(1, depths[a]);
        Assert.Equal(2, depths[b]);
        Assert.Equal(3, depths[c]);
    }

    [Fact]
    public void Star_ChildrenShareDepthOne()
    {
        Guid root = Guid.NewGuid();
        Guid a = Guid.NewGuid();
        Guid b = Guid.NewGuid();

        IReadOnlyDictionary<Guid, int> depths = MindMapHierarchy.ComputeDepths(
            new[] { Branch(root, a), Branch(root, b) });

        Assert.Equal(0, depths[root]);
        Assert.Equal(1, depths[a]);
        Assert.Equal(1, depths[b]);
    }

    [Fact]
    public void MultiplePaths_KeepsShortestDepth()
    {
        // root → a → leaf  and  root → leaf : the direct edge wins (depth 1).
        Guid root = Guid.NewGuid();
        Guid a = Guid.NewGuid();
        Guid leaf = Guid.NewGuid();

        IReadOnlyDictionary<Guid, int> depths = MindMapHierarchy.ComputeDepths(
            new[] { Branch(root, a), Branch(a, leaf), Branch(root, leaf) });

        Assert.Equal(1, depths[leaf]);
    }

    [Fact]
    public void Cycle_TerminatesAndStaysDefined()
    {
        // root → a → b → a (a cycle below the root): must terminate and define every node.
        Guid root = Guid.NewGuid();
        Guid a = Guid.NewGuid();
        Guid b = Guid.NewGuid();

        IReadOnlyDictionary<Guid, int> depths = MindMapHierarchy.ComputeDepths(
            new[] { Branch(root, a), Branch(a, b), Branch(b, a) });

        Assert.Equal(0, depths[root]);
        Assert.Equal(1, depths[a]);
        Assert.Equal(2, depths[b]);
    }

    [Fact]
    public void DepthOf_UnknownNode_IsZero()
    {
        IReadOnlyDictionary<Guid, int> depths = MindMapHierarchy.ComputeDepths(Array.Empty<Connector>());
        Assert.Equal(0, MindMapHierarchy.DepthOf(depths, Guid.NewGuid()));
    }
}
