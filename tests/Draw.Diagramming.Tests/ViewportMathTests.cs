using Draw.Diagramming.Geometry;
using Draw.Model.Primitives;
using Xunit;

namespace Draw.Diagramming.Tests;

public class ViewportMathTests
{
    [Fact]
    public void FitToContent_SmallerThanViewport_DoesNotEnlargePastFullSize()
    {
        ViewportFit fit = ViewportMath.FitToContent(
            new Rect2D(0d, 0d, 100d, 100d), viewportWidth: 800d, viewportHeight: 600d,
            minZoom: 0.1d, maxZoom: 8d, margin: 0d);

        Assert.Equal(1d, fit.Zoom, 6);
        // Centre (50,50) at zoom 1 lands at the viewport centre.
        Assert.Equal(350d, fit.PanX, 6);
        Assert.Equal(250d, fit.PanY, 6);
    }

    [Fact]
    public void FitToContent_LargerThanViewport_ScalesDownAndCentres()
    {
        ViewportFit fit = ViewportMath.FitToContent(
            new Rect2D(0d, 0d, 1000d, 1000d), viewportWidth: 500d, viewportHeight: 500d,
            minZoom: 0.1d, maxZoom: 8d, margin: 0d);

        Assert.Equal(0.5d, fit.Zoom, 6);
        Assert.Equal(0d, fit.PanX, 6);
        Assert.Equal(0d, fit.PanY, 6);
    }

    [Fact]
    public void FitToContent_ClampsToMinZoom()
    {
        ViewportFit fit = ViewportMath.FitToContent(
            new Rect2D(0d, 0d, 10000d, 10000d), viewportWidth: 100d, viewportHeight: 100d,
            minZoom: 0.1d, maxZoom: 8d, margin: 0d);

        Assert.Equal(0.1d, fit.Zoom, 6);
        Assert.Equal(50d - (5000d * 0.1d), fit.PanX, 6);
    }

    [Fact]
    public void FitToContent_AppliesMarginToContentBounds()
    {
        // Margin enlarges the fitted box, so a content box that exactly fills the viewport scales below 1.
        ViewportFit fit = ViewportMath.FitToContent(
            new Rect2D(0d, 0d, 500d, 500d), viewportWidth: 500d, viewportHeight: 500d,
            minZoom: 0.1d, maxZoom: 8d, margin: 50d);

        // Inflated box is 600×600 → fit = 500/600.
        Assert.Equal(500d / 600d, fit.Zoom, 6);
    }

    [Fact]
    public void CenterToWorld_NoPanUnitZoom_IsViewportCentre()
    {
        Point2D world = ViewportMath.CenterToWorld(800d, 600d, panX: 0d, panY: 0d, zoom: 1d);
        Assert.Equal(400d, world.X, 6);
        Assert.Equal(300d, world.Y, 6);
    }

    [Fact]
    public void CenterToWorld_AppliesPanAndZoom()
    {
        Point2D world = ViewportMath.CenterToWorld(800d, 600d, panX: 100d, panY: 50d, zoom: 2d);
        Assert.Equal(150d, world.X, 6);
        Assert.Equal(125d, world.Y, 6);
    }

    [Fact]
    public void CenterToWorld_GuardsNonPositiveZoom()
    {
        Point2D world = ViewportMath.CenterToWorld(800d, 600d, panX: 0d, panY: 0d, zoom: 0d);
        Assert.Equal(400d, world.X, 6);
        Assert.Equal(300d, world.Y, 6);
    }
}
