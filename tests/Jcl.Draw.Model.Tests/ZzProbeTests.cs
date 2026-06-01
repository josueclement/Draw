using System;
using Jcl.Draw.Model.Connectors;
using Jcl.Draw.Model.Documents;
using Jcl.Draw.Model.Primitives;
using Jcl.Draw.Model.Serialization;
using Jcl.Draw.Model.Styling;
using Xunit;

namespace Jcl.Draw.Model.Tests;

public class ZzProbeTests
{
    [Fact]
    public void Json_RoundTrips_Labels_And_Style()
    {
        var doc = new DiagramDocument();
        var c = new Connector
        {
            SourceNodeId = Guid.NewGuid(),
            TargetNodeId = Guid.NewGuid(),
            Kind = RelationshipKind.Aggregation,
            Route = RouteStyle.Orthogonal,
            SourceLabel = "src",
            TargetLabel = "tgt",
            CenterLabel = "center",
        };
        c.BendPoints.Add(new Point2D(1, 2));
        c.BendPoints.Add(new Point2D(3, 4));
        c.Style.Stroke.Thickness = 7.5;
        c.Style.Stroke.Dash = DashStyle.Dash;
        c.Style.Font.Family = "Probe";
        doc.Connectors.Add(c);

        var ser = new JsonDocumentSerializer();
        var back = ser.Deserialize(ser.Serialize(doc));
        var bc = Assert.Single(back.Connectors);

        Assert.Equal("src", bc.SourceLabel);
        Assert.Equal("tgt", bc.TargetLabel);
        Assert.Equal("center", bc.CenterLabel);
        Assert.Equal(2, bc.BendPoints.Count);
        Assert.Equal(new Point2D(3, 4), bc.BendPoints[1]);
        Assert.Equal(7.5, bc.Style.Stroke.Thickness);
        Assert.Equal(DashStyle.Dash, bc.Style.Stroke.Dash);
        Assert.Equal("Probe", bc.Style.Font.Family);
        Assert.Equal(c.SourceNodeId, bc.SourceNodeId);
        Assert.Equal(c.TargetNodeId, bc.TargetNodeId);
    }

    [Fact]
    public void ConnectorClone_Drops_Labels_Probe()
    {
        var c = new Connector
        {
            SourceLabel = "src",
            TargetLabel = "tgt",
            CenterLabel = "center",
        };
        var clone = c.Clone();
        // Demonstrates intent: these SHOULD be equal. Asserting equality to prove/disprove.
        Assert.Equal("src", clone.SourceLabel);
        Assert.Equal("tgt", clone.TargetLabel);
        Assert.Equal("center", clone.CenterLabel);
    }
}
