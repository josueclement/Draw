using Jcl.Draw.App.ViewModels;
using Jcl.Draw.Model.Nodes;
using Jcl.Draw.Model.Primitives;
using Jcl.Draw.Model.Styling;
using Xunit;

namespace Jcl.Draw.App.Tests;

public class UseCaseNodeViewModelTests
{
    [Fact]
    public void Actor_BoundaryRectangle_LabelMapsToName_HasInlineLabel()
    {
        ActorNode model = new() { Name = "Customer", Bounds = new Rect2D(0, 0, 48, 84), Style = ShapeStyle.CreateDefault() };
        ActorNodeViewModel vm = new(model);

        Assert.Equal(ShapeKind.Rectangle, vm.BoundaryKind);
        Assert.True(vm.HasInlineLabel);
        Assert.Equal("Customer", vm.Label);
        vm.Label = "Admin";
        Assert.Equal("Admin", model.Name);
    }

    [Fact]
    public void UseCase_BoundaryEllipse_LabelMapsToText()
    {
        UseCaseNode model = new() { Text = "Place order", Bounds = new Rect2D(0, 0, 130, 72), Style = ShapeStyle.CreateDefault() };
        UseCaseNodeViewModel vm = new(model);

        Assert.Equal(ShapeKind.Ellipse, vm.BoundaryKind);
        Assert.True(vm.HasInlineLabel);
        Assert.Equal("Place order", vm.Label);
        vm.Label = "Cancel order";
        Assert.Equal("Cancel order", model.Text);
    }

    [Fact]
    public void SystemBoundary_BoundaryRectangle_LabelMapsToTitle()
    {
        SystemBoundaryNode model = new() { Title = "Shop", Bounds = new Rect2D(0, 0, 320, 220), Style = ShapeStyle.CreateDefault() };
        SystemBoundaryNodeViewModel vm = new(model);

        Assert.Equal(ShapeKind.Rectangle, vm.BoundaryKind);
        Assert.True(vm.HasInlineLabel);
        Assert.Equal("Shop", vm.Label);
        vm.Label = "Store";
        Assert.Equal("Store", model.Title);
    }
}
