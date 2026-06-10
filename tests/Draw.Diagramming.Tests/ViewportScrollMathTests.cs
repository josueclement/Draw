using Draw.Diagramming.Geometry;
using Draw.Model.Primitives;
using Xunit;

namespace Draw.Diagramming.Tests;

public class ViewportScrollMathTests
{
    [Fact]
    public void ContentFitsView_NeedsNoBars()
    {
        Rect2D content = new(100, 100, 50, 50);
        Rect2D view = new(0, 0, 1000, 1000);
        ScrollBarLayout layout = ViewportScrollMath.Compute(content, view, contentMargin: 50d);
        Assert.False(layout.Horizontal.Needed);
        Assert.False(layout.Vertical.Needed);
    }

    [Fact]
    public void ContentLargerThanView_NeedsBars_WithRangeAndValue()
    {
        Rect2D content = new(0, 0, 1000, 1000);
        Rect2D view = new(0, 0, 500, 500);
        ScrollBarLayout layout = ViewportScrollMath.Compute(content, view, contentMargin: 50d);

        Assert.True(layout.Horizontal.Needed);
        Assert.Equal(0d, layout.Horizontal.Minimum, 6);
        Assert.Equal(600d, layout.Horizontal.Maximum, 6);      // region width 1100 - view 500
        Assert.Equal(500d, layout.Horizontal.ViewportSize, 6);
        Assert.Equal(50d, layout.Horizontal.Value, 6);          // viewLeft 0 - regionLeft -50
        Assert.Equal(-50d, layout.Region.Left, 6);
    }

    [Fact]
    public void Value_IsClampedToMaximum_WhenPannedPastContent()
    {
        Rect2D content = new(0, 0, 1000, 1000);
        Rect2D view = new(5000, 0, 500, 500); // far right of content + margin
        ScrollBarLayout layout = ViewportScrollMath.Compute(content, view, contentMargin: 50d);
        Assert.Equal(layout.Horizontal.Maximum, layout.Horizontal.Value, 6);
    }

    [Fact]
    public void PanForScrollValue_InvertsOrigin()
    {
        Assert.Equal(0d, ViewportScrollMath.PanForScrollValue(regionStart: -50d, scrollValue: 50d, zoom: 1d), 6);
        Assert.Equal(-30d, ViewportScrollMath.PanForScrollValue(regionStart: 10d, scrollValue: 5d, zoom: 2d), 6);
    }

    [Fact]
    public void PanForScrollValue_RoundTripsWithComputedValue()
    {
        // Compute a scrollbar value from a pan, invert it, and confirm the original pan is recovered.
        double zoom = 2d;
        double panX = -120d;
        Rect2D content = new(0, 0, 800, 600);
        double viewLeft = -panX / zoom; // how the view derives the visible region from pan
        Rect2D view = new(viewLeft, 0, 300, 300);
        ScrollBarLayout layout = ViewportScrollMath.Compute(content, view, contentMargin: 50d);
        double recoveredPan = ViewportScrollMath.PanForScrollValue(layout.Region.Left, layout.Horizontal.Value, zoom);
        Assert.Equal(panX, recoveredPan, 6);
    }
}
