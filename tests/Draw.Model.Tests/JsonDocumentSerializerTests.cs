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
            Route = RouteStyle.Bezier,
        };
        connector.BendPoints.Add(new Point2D(5, 6));
        doc.Connectors.Add(connector);

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
        Assert.Equal(RouteStyle.Bezier, connector.Route);
        Assert.Equal(new Point2D(5, 6), Assert.Single(connector.BendPoints));
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
    public void Deserialize_NewerSchema_Throws()
    {
        JsonDocumentSerializer serializer = new();

        Assert.Throws<UnsupportedSchemaVersionException>(
            () => serializer.Deserialize("{\"schemaVersion\":999}"));
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
}
