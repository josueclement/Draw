using Jcl.Draw.Model.Nodes;
using Xunit;

namespace Jcl.Draw.Model.Tests;

public class ClassMemberTests
{
    [Fact]
    public void Clone_CopiesAllFields_AndIsIndependent()
    {
        ClassMember member = new()
        {
            Visibility = MemberVisibility.Protected,
            Name = "deposit",
            Type = "void",
            Parameters = "amount: decimal",
            Kind = MemberKind.Operation,
            IsStatic = true,
            IsAbstract = true,
        };

        ClassMember clone = member.Clone();
        clone.Name = "withdraw";

        Assert.Equal("deposit", member.Name);
        Assert.Equal("withdraw", clone.Name);
        Assert.Equal(MemberVisibility.Protected, clone.Visibility);
        Assert.Equal("void", clone.Type);
        Assert.Equal("amount: decimal", clone.Parameters);
        Assert.Equal(MemberKind.Operation, clone.Kind);
        Assert.True(clone.IsStatic);
        Assert.True(clone.IsAbstract);
    }
}
