using System;
using Draw.Model.Connectors;
using Draw.Model.Documents;
using Draw.Model.Nodes;
using Draw.Model.Primitives;
using Draw.Model.Serialization;
using Draw.Model.Styling;
using Xunit;

namespace Draw.Model.Tests;

public class JsonDocumentSerializerTests
{
    private static DiagramDocument BuildSample(out Guid nodeId)
    {
        DiagramDocument doc = new() { DiagramType = DiagramType.Class, Title = "Sample" };

        ShapeNode node = new()
        {
            Id = Guid.NewGuid(),
            Kind = ShapeKind.RoundedRectangle,
            Text = "Hello",
            CornerRadius = 8,
            Bounds = new Rect2D(10, 20, 100, 40),
        };
        node.Style.Fill = ArgbColor.FromRgb(0x11, 0x22, 0x33);
        node.Style.Stroke.Thickness = 3;
        doc.Nodes.Add(node);
        nodeId = node.Id;

        Connector connector = new()
        {
            SourceNodeId = node.Id,
            TargetNodeId = Guid.NewGuid(),
            Kind = RelationshipKind.Composition,
            Route = RouteStyle.Rounded,
        };
        connector.BendPoints.Add(new Point2D(5, 6));
        doc.Connectors.Add(connector);

        return doc;
    }

    // One node of every polymorphic $type, plus a connector, for full discriminator coverage.
    private static DiagramDocument BuildAllNodeTypes()
    {
        DiagramDocument doc = new() { DiagramType = DiagramType.Class, Title = "All types" };

        doc.Nodes.Add(new ShapeNode
        {
            Kind = ShapeKind.Diamond, Text = "shape", CornerRadius = 4, Bounds = new Rect2D(0, 0, 10, 10),
        });

        ClassNode classNode = new()
        {
            Kind = ClassNodeKind.Interface, Name = "IRepo", IsAbstract = true, Bounds = new Rect2D(20, 0, 10, 10),
        };
        classNode.Members.Add(new ClassMember
        {
            Visibility = MemberVisibility.Protected, Name = "count", Type = "int", Kind = MemberKind.Field, IsStatic = true,
        });
        classNode.Members.Add(new ClassMember
        {
            Visibility = MemberVisibility.Public, Name = "save", Parameters = "x: T", Type = "void",
            Kind = MemberKind.Operation, IsAbstract = true,
        });
        classNode.Members.Add(new ClassMember { Name = "ACTIVE", Kind = MemberKind.EnumLiteral });
        doc.Nodes.Add(classNode);

        doc.Nodes.Add(new ActorNode { Name = "User", Bounds = new Rect2D(40, 0, 10, 10) });
        doc.Nodes.Add(new UseCaseNode { Text = "Checkout", Bounds = new Rect2D(60, 0, 10, 10) });
        doc.Nodes.Add(new SystemBoundaryNode { Title = "System", Bounds = new Rect2D(80, 0, 10, 10) });
        doc.Nodes.Add(new ImageNode
        {
            Data = new byte[] { 1, 2, 3 }, Format = "jpeg", PixelWidth = 4, PixelHeight = 5, Bounds = new Rect2D(100, 0, 10, 10),
        });

        EntityNode entity = new() { Name = "users", Bounds = new Rect2D(120, 0, 10, 10) };
        entity.Columns.Add(new EntityColumn
        {
            Name = "id", Type = "int", IsPrimaryKey = true, IsForeignKey = false, IsUnique = true, IsNullable = false,
        });
        doc.Nodes.Add(entity);

        return doc;
    }

    [Fact]
    public void RoundTrip_PreservesPolymorphicNodeAndStyle()
    {
        JsonDocumentSerializer serializer = new();
        DiagramDocument doc = BuildSample(out Guid nodeId);

        DiagramDocument back = serializer.Deserialize(serializer.Serialize(doc));

        Assert.Equal(DiagramType.Class, back.DiagramType);
        ShapeNode node = Assert.IsType<ShapeNode>(Assert.Single(back.Nodes));
        Assert.Equal(nodeId, node.Id);
        Assert.Equal(ShapeKind.RoundedRectangle, node.Kind);
        Assert.Equal("Hello", node.Text);
        Assert.Equal(8, node.CornerRadius);
        Assert.Equal(new Rect2D(10, 20, 100, 40), node.Bounds);
        Assert.Equal(ArgbColor.FromRgb(0x11, 0x22, 0x33), node.Style.Fill);
        Assert.Equal(3, node.Style.Stroke.Thickness);
    }

    [Fact]
    public void RoundTrip_PreservesConnector()
    {
        JsonDocumentSerializer serializer = new();
        DiagramDocument doc = BuildSample(out _);

        DiagramDocument back = serializer.Deserialize(serializer.Serialize(doc));

        Connector connector = Assert.Single(back.Connectors);
        Assert.Equal(RelationshipKind.Composition, connector.Kind);
        Assert.Equal(RouteStyle.Rounded, connector.Route);
        Assert.Equal(new Point2D(5, 6), Assert.Single(connector.BendPoints));
    }

    [Fact]
    public void NewConnector_DefaultsToGrayStroke_WhileNodeOutlineStaysBlue()
    {
        Assert.Equal(ConnectorStyle.DefaultStrokeColor, new Connector().Style.Stroke.Color);
        Assert.Equal(StrokeStyle.DefaultColor, new ShapeNode().Style.Stroke.Color);
        Assert.NotEqual(StrokeStyle.DefaultColor, ConnectorStyle.DefaultStrokeColor);
    }

    [Fact]
    public void RoundTrip_PreservesDefaultGrayConnectorStroke()
    {
        JsonDocumentSerializer serializer = new();
        DiagramDocument doc = new();
        doc.Connectors.Add(new Connector { SourceNodeId = Guid.NewGuid(), TargetNodeId = Guid.NewGuid() });

        DiagramDocument back = serializer.Deserialize(serializer.Serialize(doc));

        Assert.Equal(ConnectorStyle.DefaultStrokeColor, Assert.Single(back.Connectors).Style.Stroke.Color);
    }

    [Fact]
    public void RoundTrip_PreservesMindMapBranchConnectorKind()
    {
        JsonDocumentSerializer serializer = new();
        DiagramDocument doc = new();
        doc.Connectors.Add(new Connector
        {
            SourceNodeId = Guid.NewGuid(),
            TargetNodeId = Guid.NewGuid(),
            Kind = RelationshipKind.MindMapBranch,
        });

        DiagramDocument back = serializer.Deserialize(serializer.Serialize(doc));

        Assert.Equal(RelationshipKind.MindMapBranch, Assert.Single(back.Connectors).Kind);
    }

    [Fact]
    public void RoundTrip_PreservesNodeMarkers_InOrder()
    {
        JsonDocumentSerializer serializer = new();
        ShapeNode node = new() { Kind = ShapeKind.Rectangle, Bounds = new Rect2D(0, 0, 10, 10) };
        node.Markers.Add(NodeMarker.Todo);
        node.Markers.Add(NodeMarker.Done);
        node.Markers.Add(NodeMarker.Important);
        DiagramDocument doc = new() { Nodes = { node } };

        DiagramDocument back = serializer.Deserialize(serializer.Serialize(doc));

        ShapeNode result = Assert.IsType<ShapeNode>(Assert.Single(back.Nodes));
        Assert.Equal(new[] { NodeMarker.Todo, NodeMarker.Done, NodeMarker.Important }, result.Markers);
    }

    [Fact]
    public void Serialize_WritesMarkersAsStrings()
    {
        JsonDocumentSerializer serializer = new();
        ShapeNode node = new();
        node.Markers.Add(NodeMarker.Stuck);
        DiagramDocument doc = new() { Nodes = { node } };

        string json = serializer.Serialize(doc);

        Assert.Contains("\"Stuck\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void RoundTrip_PreservesGeneralPurposeIcon()
    {
        JsonDocumentSerializer serializer = new();
        ShapeNode node = new();
        node.Markers.Add(NodeMarker.Island);
        DiagramDocument doc = new() { Nodes = { node } };

        string json = serializer.Serialize(doc);
        DiagramDocument back = serializer.Deserialize(json);

        Assert.Contains("\"Island\"", json, StringComparison.Ordinal);
        Assert.Equal(new[] { NodeMarker.Island }, Assert.IsType<ShapeNode>(Assert.Single(back.Nodes)).Markers);
    }

    [Fact]
    public void Clone_NodeMarkers_AreIndependent()
    {
        JsonDocumentSerializer serializer = new();
        ShapeNode node = new();
        node.Markers.Add(NodeMarker.Idea);
        DiagramDocument doc = new() { Nodes = { node } };

        DiagramDocument clone = serializer.Clone(doc);
        Assert.IsType<ShapeNode>(clone.Nodes[0]).Markers.Add(NodeMarker.Question);

        Assert.Equal(new[] { NodeMarker.Idea }, Assert.IsType<ShapeNode>(doc.Nodes[0]).Markers);
    }

    [Fact]
    public void RoundTrip_PreservesEveryNodeType()
    {
        JsonDocumentSerializer serializer = new();
        DiagramDocument doc = BuildAllNodeTypes();

        DiagramDocument back = serializer.Deserialize(serializer.Serialize(doc));

        Assert.Equal(7, back.Nodes.Count);

        ShapeNode shape = Assert.IsType<ShapeNode>(back.Nodes[0]);
        Assert.Equal(ShapeKind.Diamond, shape.Kind);

        ClassNode classNode = Assert.IsType<ClassNode>(back.Nodes[1]);
        Assert.Equal(ClassNodeKind.Interface, classNode.Kind);
        Assert.True(classNode.IsAbstract);
        Assert.Equal(3, classNode.Members.Count);
        Assert.Equal(MemberKind.Operation, classNode.Members[1].Kind);
        Assert.True(classNode.Members[1].IsAbstract);
        Assert.Equal("x: T", classNode.Members[1].Parameters);
        Assert.True(classNode.Members[0].IsStatic);
        Assert.Equal(MemberKind.EnumLiteral, classNode.Members[2].Kind);

        Assert.Equal("User", Assert.IsType<ActorNode>(back.Nodes[2]).Name);
        Assert.Equal("Checkout", Assert.IsType<UseCaseNode>(back.Nodes[3]).Text);
        Assert.Equal("System", Assert.IsType<SystemBoundaryNode>(back.Nodes[4]).Title);

        ImageNode image = Assert.IsType<ImageNode>(back.Nodes[5]);
        Assert.Equal(new byte[] { 1, 2, 3 }, image.Data);
        Assert.Equal("jpeg", image.Format);
        Assert.Equal(4, image.PixelWidth);

        EntityNode entity = Assert.IsType<EntityNode>(back.Nodes[6]);
        EntityColumn column = Assert.Single(entity.Columns);
        Assert.True(column.IsPrimaryKey);
        Assert.True(column.IsUnique);
        Assert.False(column.IsNullable);
    }

    [Theory]
    [InlineData("shape")]
    [InlineData("class")]
    [InlineData("actor")]
    [InlineData("useCase")]
    [InlineData("systemBoundary")]
    [InlineData("image")]
    [InlineData("entity")]
    public void Serialize_EmitsTypeDiscriminator(string discriminator)
    {
        JsonDocumentSerializer serializer = new();

        string json = serializer.Serialize(BuildAllNodeTypes());

        Assert.Contains($"\"$type\": \"{discriminator}\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_WritesColorAsHexString()
    {
        JsonDocumentSerializer serializer = new();
        DiagramDocument doc = BuildSample(out _);

        string json = serializer.Serialize(doc);

        Assert.Contains("#FF112233", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RoundTrip_IsJsonStable()
    {
        JsonDocumentSerializer serializer = new();
        DiagramDocument doc = BuildAllNodeTypes();

        string first = serializer.Serialize(doc);
        string second = serializer.Serialize(serializer.Deserialize(first));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Deserialize_NewerSchema_Throws()
    {
        JsonDocumentSerializer serializer = new();

        Assert.Throws<UnsupportedSchemaVersionException>(
            () => serializer.Deserialize("{\"schemaVersion\":999}"));
    }

    [Fact]
    public void Deserialize_SchemaVersionJustAboveCurrent_Throws()
    {
        JsonDocumentSerializer serializer = new();
        string json = $"{{\"schemaVersion\":{DocumentSchema.CurrentVersion + 1}}}";

        Assert.Throws<UnsupportedSchemaVersionException>(() => serializer.Deserialize(json));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":0}")]
    [InlineData("{\"schemaVersion\":1}")]
    public void Deserialize_CurrentOrOlderSchema_Succeeds(string json)
    {
        JsonDocumentSerializer serializer = new();

        DiagramDocument doc = serializer.Deserialize(json);

        Assert.NotNull(doc);
        Assert.True(doc.SchemaVersion <= DocumentSchema.CurrentVersion);
    }

    [Fact]
    public void Deserialize_OlderSchema_IsStampedToCurrentVersion()
    {
        // The migration seam upgrades an older document to the current shape and stamps the version,
        // so downstream code never sees a stale SchemaVersion (the v1→v2 colour transform is below).
        JsonDocumentSerializer serializer = new();

        DiagramDocument doc = serializer.Deserialize("{\"schemaVersion\":0}");

        Assert.Equal(DocumentSchema.CurrentVersion, doc.SchemaVersion);
    }

    [Fact]
    public void Migrate_V1DefaultColours_BecomeNull_ToFollowTheme()
    {
        // A v1 document stored the theme-following default fill/text as their literal colours; v2
        // represents "follow the theme" as null, so migration must null those legacy defaults.
        JsonDocumentSerializer serializer = new();
        ShapeNode node = new() { Style = new ShapeStyle { Fill = ShapeStyle.DefaultFill } };
        node.Style.Font.Color = FontSpec.DefaultColor;
        DiagramDocument v1 = new() { SchemaVersion = 1, Nodes = { node } };

        DiagramDocument migrated = serializer.Deserialize(serializer.Serialize(v1));

        ShapeNode result = Assert.IsType<ShapeNode>(migrated.Nodes[0]);
        Assert.Null(result.Style.Fill);
        Assert.Null(result.Style.Font.Color);
        Assert.Equal(DocumentSchema.CurrentVersion, migrated.SchemaVersion);
    }

    [Fact]
    public void Migrate_V1CustomFill_IsPreserved()
    {
        JsonDocumentSerializer serializer = new();
        ArgbColor custom = ArgbColor.FromRgb(0x11, 0x22, 0x33);
        ShapeNode node = new() { Style = new ShapeStyle { Fill = custom } };
        DiagramDocument v1 = new() { SchemaVersion = 1, Nodes = { node } };

        DiagramDocument migrated = serializer.Deserialize(serializer.Serialize(v1));

        Assert.Equal(custom, Assert.IsType<ShapeNode>(migrated.Nodes[0]).Style.Fill);
    }

    [Fact]
    public void RoundTrip_NullFill_IsOmittedFromJson_AndReadsBackAsNull()
    {
        JsonDocumentSerializer serializer = new();
        DiagramDocument doc = new() { Nodes = { new ShapeNode() } }; // default style → null fill

        string json = serializer.Serialize(doc);
        Assert.DoesNotContain("\"fill\"", json, StringComparison.OrdinalIgnoreCase);

        DiagramDocument round = serializer.Deserialize(json);
        Assert.Null(Assert.IsType<ShapeNode>(round.Nodes[0]).Style.Fill);
    }

    [Fact]
    public void RoundTrip_V2_ExplicitFillEqualToDefault_StaysNonNull()
    {
        // The bug C1 fixes: in v2 a user may pick exactly the default colour as a custom fill. It must
        // round-trip as a concrete (non-null) colour, not be re-interpreted as "follow the theme".
        // Migration runs only for v1, so a current-version document is never re-nulled.
        JsonDocumentSerializer serializer = new();
        ShapeNode node = new() { Style = new ShapeStyle { Fill = ShapeStyle.DefaultFill } };
        DiagramDocument v2 = new() { Nodes = { node } }; // SchemaVersion defaults to current

        DiagramDocument round = serializer.Deserialize(serializer.Serialize(v2));

        Assert.Equal(ShapeStyle.DefaultFill, Assert.IsType<ShapeNode>(round.Nodes[0]).Style.Fill);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not json")]
    public void Deserialize_InvalidContent_ThrowsInvalidDocument(string json)
    {
        JsonDocumentSerializer serializer = new();

        Assert.Throws<InvalidDocumentException>(() => serializer.Deserialize(json));
    }

    [Fact]
    public void Deserialize_NullLiteral_ThrowsInvalidDocument()
    {
        JsonDocumentSerializer serializer = new();

        Assert.Throws<InvalidDocumentException>(() => serializer.Deserialize("null"));
    }

    [Fact]
    public void Clone_IsDeepAndIndependent()
    {
        JsonDocumentSerializer serializer = new();
        DiagramDocument doc = BuildSample(out _);

        DiagramDocument clone = serializer.Clone(doc);
        clone.Nodes[0].Bounds = new Rect2D(999, 999, 1, 1);

        Assert.NotSame(doc, clone);
        Assert.Equal(new Rect2D(10, 20, 100, 40), doc.Nodes[0].Bounds);
        Assert.Equal(new Rect2D(999, 999, 1, 1), clone.Nodes[0].Bounds);
    }

    [Fact]
    public void Clone_NestedCollectionsAndStyle_AreIndependent()
    {
        JsonDocumentSerializer serializer = new();
        DiagramDocument doc = BuildAllNodeTypes();

        DiagramDocument clone = serializer.Clone(doc);
        ClassNode clonedClass = Assert.IsType<ClassNode>(clone.Nodes[1]);
        EntityNode clonedEntity = Assert.IsType<EntityNode>(clone.Nodes[6]);
        clonedClass.Members[0].Name = "MUTATED";
        clonedEntity.Columns[0].Name = "MUTATED";
        clone.Nodes[0].Style.Stroke.Thickness = 99;

        ClassNode originalClass = Assert.IsType<ClassNode>(doc.Nodes[1]);
        EntityNode originalEntity = Assert.IsType<EntityNode>(doc.Nodes[6]);
        Assert.Equal("count", originalClass.Members[0].Name);
        Assert.Equal("id", originalEntity.Columns[0].Name);
        Assert.NotEqual(99, doc.Nodes[0].Style.Stroke.Thickness);
    }
}
