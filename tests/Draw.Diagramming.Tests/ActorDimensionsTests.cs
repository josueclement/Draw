using Draw.Diagramming.Geometry;
using Xunit;

namespace Draw.Diagramming.Tests;

public class ActorDimensionsTests
{
    [Fact]
    public void ComputesExpectedProportions_ForRepresentativeBounds()
    {
        ActorDimensions d = new(100, 120);

        // labelStrip = min(18, 120*0.25) = 18 -> figureHeight = 102.
        Assert.Equal(102d, d.FigureHeight, 3);
        Assert.Equal(50d, d.CenterX, 3);
        Assert.Equal(18d, d.HeadRadius, 3); // min(100, 102) * 0.18
        Assert.Equal(36d, d.NeckY, 3);
        Assert.Equal(63.24d, d.HipY, 3);
        Assert.Equal(42.81d, d.ShoulderY, 3);
        Assert.Equal(30d, d.ArmHalf, 3);
        Assert.Equal(28d, d.LegSpread, 3);
    }

    [Fact]
    public void ClampsDegenerateBounds_ToAtLeastOnePixel()
    {
        ActorDimensions d = new(0.2, 0.2);

        // Width/height clamp to 1, so the figure is built as if 1x1.
        Assert.Equal(0.5d, d.CenterX, 3);
        Assert.Equal(1d, d.FigureHeight, 3);
        Assert.Equal(0.18d, d.HeadRadius, 3);
    }
}
