using System.Collections.Generic;
using Jcl.Draw.App.ViewModels;
using Jcl.Draw.Model.Nodes;
using Xunit;

namespace Jcl.Draw.App.Tests;

public class ClassMemberViewModelTests
{
    private sealed class FakeContext : INodeEditContext
    {
        public int Begins { get; private set; }
        public int Ends { get; private set; }
        public void BeginMemberEdit() => Begins++;
        public void EndMemberEdit() => Ends++;
        public IReadOnlyList<string> GetTypeSuggestions() => new[] { "int", "string" };
    }

    [Fact]
    public void SettingName_WritesThrough_AndBracketsWithContext()
    {
        ClassMember model = new() { Name = "x", Kind = MemberKind.Field };
        FakeContext ctx = new();
        ClassMemberViewModel vm = new(model, ctx);

        vm.Name = "balance";

        Assert.Equal("balance", model.Name);
        Assert.Equal(1, ctx.Begins);
        Assert.Equal(1, ctx.Ends);
    }

    [Fact]
    public void DisplayText_ReflectsModel()
    {
        ClassMember model = new() { Visibility = MemberVisibility.Private, Name = "id", Type = "Guid", Kind = MemberKind.Field };
        ClassMemberViewModel vm = new(model, new FakeContext());

        Assert.Equal("- id: Guid", vm.DisplayText);
    }

    [Fact]
    public void CommitEdit_ParsesRawText_IntoModel()
    {
        ClassMember model = new() { Name = "old", Kind = MemberKind.Field };
        ClassMemberViewModel vm = new(model, new FakeContext());

        vm.BeginEdit();
        vm.RawText = "+ deposit(amount: decimal): void";
        vm.CommitEdit();

        Assert.False(vm.IsEditing);
        Assert.Equal(MemberKind.Operation, model.Kind);
        Assert.Equal("deposit", model.Name);
        Assert.Equal("amount: decimal", model.Parameters);
        Assert.Equal("void", model.Type);
    }

    [Fact]
    public void EnumLiteralContext_CommitKeepsLiteralKind()
    {
        ClassMember model = new() { Name = "ACTIVE", Kind = MemberKind.EnumLiteral };
        ClassMemberViewModel vm = new(model, new FakeContext());

        vm.BeginEdit();
        vm.RawText = "INACTIVE";
        vm.CommitEdit();

        Assert.Equal(MemberKind.EnumLiteral, model.Kind);
        Assert.Equal("INACTIVE", model.Name);
    }
}
