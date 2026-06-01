using System;
using System.Collections.Generic;
using Jcl.Draw.Model.Documents;
using Jcl.Draw.Model.Nodes;
using Jcl.Draw.Model.Primitives;
using Jcl.Draw.Model.Serialization;
using Xunit;

namespace Jcl.Draw.Model.Tests;

public class ClassNodeTests
{
    private static ClassNode SampleClassNode() => new()
    {
        Id = Guid.NewGuid(),
        Kind = ClassNodeKind.Interface,
        Name = "Account",
        IsAbstract = true,
        Bounds = new Rect2D(10, 20, 160, 100),
        Members = new List<ClassMember>
        {
            new() { Visibility = MemberVisibility.Private, Name = "id", Type = "Guid", Kind = MemberKind.Field },
            new() { Visibility = MemberVisibility.Public, Name = "deposit", Type = "void", Parameters = "amount: decimal", Kind = MemberKind.Operation },
        },
    };

    [Fact]
    public void Clone_DeepCopiesMembers()
    {
        ClassNode node = SampleClassNode();

        ClassNode clone = Assert.IsType<ClassNode>(node.Clone());
        clone.Members[0].Name = "changed";

        Assert.Equal(node.Id, clone.Id);
        Assert.Equal("id", node.Members[0].Name);
        Assert.Equal("changed", clone.Members[0].Name);
        Assert.NotSame(node.Members[0], clone.Members[0]);
    }

    [Fact]
    public void RoundTrip_PreservesClassNodeAndMembers()
    {
        JsonDocumentSerializer serializer = new();
        DiagramDocument doc = new() { DiagramType = DiagramType.Class };
        doc.Nodes.Add(SampleClassNode());

        DiagramDocument back = serializer.Deserialize(serializer.Serialize(doc));

        ClassNode node = Assert.IsType<ClassNode>(Assert.Single(back.Nodes));
        Assert.Equal(ClassNodeKind.Interface, node.Kind);
        Assert.Equal("Account", node.Name);
        Assert.True(node.IsAbstract);
        Assert.Equal(2, node.Members.Count);
        Assert.Equal(MemberKind.Operation, node.Members[1].Kind);
        Assert.Equal("amount: decimal", node.Members[1].Parameters);
    }
}
