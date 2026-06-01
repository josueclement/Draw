using Jcl.Draw.Diagramming.Uml;
using Jcl.Draw.Model.Nodes;
using Xunit;

namespace Jcl.Draw.Diagramming.Tests;

public class MemberSignatureTests
{
    [Fact]
    public void Format_Field_WithType()
    {
        ClassMember m = new() { Visibility = MemberVisibility.Private, Name = "balance", Type = "decimal", Kind = MemberKind.Field };
        Assert.Equal("- balance: decimal", MemberSignature.Format(m));
    }

    [Fact]
    public void Format_Field_WithoutType_OmitsColon()
    {
        ClassMember m = new() { Visibility = MemberVisibility.Public, Name = "id", Kind = MemberKind.Field };
        Assert.Equal("+ id", MemberSignature.Format(m));
    }

    [Fact]
    public void Format_Operation_WithParamsAndReturn()
    {
        ClassMember m = new()
        {
            Visibility = MemberVisibility.Public, Name = "deposit",
            Parameters = "amount: decimal", Type = "void", Kind = MemberKind.Operation,
        };
        Assert.Equal("+ deposit(amount: decimal): void", MemberSignature.Format(m));
    }

    [Fact]
    public void Format_Operation_NoParams_NoReturn()
    {
        ClassMember m = new() { Visibility = MemberVisibility.Protected, Name = "close", Kind = MemberKind.Operation };
        Assert.Equal("# close()", MemberSignature.Format(m));
    }

    [Fact]
    public void Format_EnumLiteral_NameOnly()
    {
        ClassMember m = new() { Name = "ACTIVE", Kind = MemberKind.EnumLiteral };
        Assert.Equal("ACTIVE", MemberSignature.Format(m));
    }
}
