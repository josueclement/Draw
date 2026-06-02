using Jcl.Draw.Model.Styling;
using Xunit;

namespace Jcl.Draw.Model.Tests;

public class ShapeStyleTests
{
    [Fact]
    public void CreateDefault_UsesCarbonAccentPalette()
    {
        ShapeStyle style = ShapeStyle.CreateDefault();

        Assert.Equal(new ArgbColor(0xFF, 0xEB, 0xED, 0xF0), style.Fill);
        Assert.Equal(new ArgbColor(0xFF, 0x35, 0x74, 0xF0), style.Stroke.Color);
        Assert.Equal(new ArgbColor(0xFF, 0x1E, 0x1F, 0x22), style.Font.Color);
        Assert.Equal(1.5d, style.Stroke.Thickness);
    }

    [Fact]
    public void NewConnectorStyle_DefaultsToAccentStroke()
    {
        ConnectorStyle style = new();

        Assert.Equal(new ArgbColor(0xFF, 0x35, 0x74, 0xF0), style.Stroke.Color);
    }
}
