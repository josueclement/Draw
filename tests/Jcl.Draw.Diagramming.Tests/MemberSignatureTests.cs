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

    [Theory]
    [InlineData("- balance: decimal", MemberVisibility.Private, "balance", "decimal", MemberKind.Field)]
    [InlineData("id", MemberVisibility.Public, "id", null, MemberKind.Field)]
    [InlineData("  + name : string ", MemberVisibility.Public, "name", "string", MemberKind.Field)]
    public void Parse_Field(string text, MemberVisibility vis, string name, string? type, MemberKind kind)
    {
        ClassMember m = MemberSignature.Parse(text, MemberKind.Field);
        Assert.Equal(vis, m.Visibility);
        Assert.Equal(name, m.Name);
        Assert.Equal(type, m.Type);
        Assert.Equal(kind, m.Kind);
    }

    [Fact]
    public void Parse_Operation_WithParamsAndReturn()
    {
        ClassMember m = MemberSignature.Parse("+ deposit(amount: decimal): void", MemberKind.Field);
        Assert.Equal(MemberKind.Operation, m.Kind);
        Assert.Equal("deposit", m.Name);
        Assert.Equal("amount: decimal", m.Parameters);
        Assert.Equal("void", m.Type);
    }

    [Fact]
    public void Parse_Operation_NoReturn()
    {
        ClassMember m = MemberSignature.Parse("# close()", MemberKind.Field);
        Assert.Equal(MemberKind.Operation, m.Kind);
        Assert.Equal("close", m.Name);
        Assert.Equal(string.Empty, m.Parameters);
        Assert.Null(m.Type);
    }

    [Fact]
    public void Parse_EnumLiteralContext_KeepsName()
    {
        ClassMember m = MemberSignature.Parse("ACTIVE", MemberKind.EnumLiteral);
        Assert.Equal(MemberKind.EnumLiteral, m.Kind);
        Assert.Equal("ACTIVE", m.Name);
    }

    [Theory]
    [InlineData("- balance: decimal", MemberKind.Field)]
    [InlineData("+ deposit(amount: decimal): void", MemberKind.Field)]
    [InlineData("# close()", MemberKind.Field)]
    [InlineData("ACTIVE", MemberKind.EnumLiteral)]
    public void Parse_RoundTripsWithFormat(string text, MemberKind context)
    {
        ClassMember m = MemberSignature.Parse(text, context);
        Assert.Equal(text, MemberSignature.Format(m));
    }
}
