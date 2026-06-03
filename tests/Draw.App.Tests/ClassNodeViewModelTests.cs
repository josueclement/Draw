using System.Collections.Generic;
using System.Linq;
using Draw.App.ViewModels;
using Draw.Model.Nodes;
using Draw.Model.Primitives;
using Draw.Model.Styling;
using Xunit;

namespace Draw.App.Tests;

public class ClassNodeViewModelTests
{
    private sealed class FakeContext : INodeEditContext
    {
        public int Begins { get; private set; }
        public int Ends { get; private set; }
        public void BeginMemberEdit() => Begins++;
        public void EndMemberEdit() => Ends++;
        public IReadOnlyList<string> GetTypeSuggestions() => new[] { "int" };
    }

    private static ClassNode Model(ClassNodeKind kind = ClassNodeKind.Class) => new()
    {
        Kind = kind,
        Name = "Account",
        Bounds = new Rect2D(0, 0, 160, 100),
        Style = ShapeStyle.CreateDefault(),
        Members = new List<ClassMember>
        {
            new() { Name = "id", Type = "Guid", Kind = MemberKind.Field },
            new() { Name = "deposit", Kind = MemberKind.Operation },
        },
    };

    [Fact]
    public void BoundaryKind_IsRectangle()
    {
        ClassNodeViewModel vm = new(Model(), new FakeContext());
        Assert.Equal(ShapeKind.Rectangle, vm.BoundaryKind);
    }

    [Fact]
    public void SplitsMembers_IntoAttributesAndOperations()
    {
        ClassNodeViewModel vm = new(Model(), new FakeContext());
        Assert.Single(vm.PrimaryMembers);
        Assert.Equal("id", vm.PrimaryMembers[0].Name);
        Assert.Single(vm.Operations);
        Assert.Equal("deposit", vm.Operations[0].Name);
        Assert.True(vm.HasOperations);
    }

    [Fact]
    public void EnumNode_HasNoOperations_AndLiteralsArePrimary()
    {
        ClassNode model = Model(ClassNodeKind.Enum);
        model.Members = new List<ClassMember> { new() { Name = "ACTIVE", Kind = MemberKind.EnumLiteral } };
        ClassNodeViewModel vm = new(model, new FakeContext());

        Assert.False(vm.HasOperations);
        Assert.True(vm.IsEnum);
        Assert.Equal("ACTIVE", vm.PrimaryMembers[0].Name);
        Assert.Equal("«enumeration»", vm.Stereotype);
    }

    [Fact]
    public void AddPrimaryMember_AddsToModelAndCollection_AndCapturesUndo()
    {
        ClassNode model = Model();
        FakeContext ctx = new();
        ClassNodeViewModel vm = new(model, ctx);

        vm.AddPrimaryMember();

        Assert.Equal(2, vm.PrimaryMembers.Count);
        Assert.Equal(3, model.Members.Count);
        Assert.True(ctx.Begins >= 1);
        Assert.True(ctx.Ends >= 1);
    }

    [Fact]
    public void RemoveMember_RemovesFromModelAndCollection()
    {
        ClassNode model = Model();
        ClassNodeViewModel vm = new(model, new FakeContext());
        ClassMemberViewModel op = vm.Operations[0];

        vm.RemoveMember(op);

        Assert.Empty(vm.Operations);
        Assert.DoesNotContain(op.Model, model.Members);
    }

    [Fact]
    public void Interface_Stereotype()
    {
        ClassNodeViewModel vm = new(Model(ClassNodeKind.Interface), new FakeContext());
        Assert.Equal("«interface»", vm.Stereotype);
        Assert.True(vm.HasStereotype);
    }
}
