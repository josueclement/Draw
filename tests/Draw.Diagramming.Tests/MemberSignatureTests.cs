using Draw.Diagramming.Uml;
using Draw.Model.Nodes;
using Xunit;

namespace Draw.Diagramming.Tests;

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
    [InlineData(MemberVisibility.Public, "+")]
    [InlineData(MemberVisibility.Private, "-")]
    [InlineData(MemberVisibility.Protected, "#")]
    [InlineData(MemberVisibility.Package, "~")]
    public void Format_EmitsVisibilityMarker(MemberVisibility visibility, string marker)
    {
        ClassMember m = new() { Visibility = visibility, Name = "x", Kind = MemberKind.Field };
        Assert.Equal($"{marker} x", MemberSignature.Format(m));
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

    [Theory]
    [InlineData('+', MemberVisibility.Public)]
    [InlineData('-', MemberVisibility.Private)]
    [InlineData('#', MemberVisibility.Protected)]
    [InlineData('~', MemberVisibility.Package)]
    public void Parse_VisibilityMarkers(char marker, MemberVisibility expected)
    {
        ClassMember m = MemberSignature.Parse($"{marker} field", MemberKind.Field);
        Assert.Equal(expected, m.Visibility);
        Assert.Equal("field", m.Name);
    }

    [Fact]
    public void Parse_NoMarker_DefaultsToPublic()
    {
        ClassMember m = MemberSignature.Parse("count: int", MemberKind.Field);
        Assert.Equal(MemberVisibility.Public, m.Visibility);
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
    [InlineData("+ id", MemberKind.Field)]
    [InlineData("~ count: int", MemberKind.Field)]
    [InlineData("+ deposit(amount: decimal): void", MemberKind.Field)]
    [InlineData("# close()", MemberKind.Field)]
    [InlineData("ACTIVE", MemberKind.EnumLiteral)]
    public void Parse_RoundTripsWithFormat(string text, MemberKind context)
    {
        ClassMember m = MemberSignature.Parse(text, context);
        Assert.Equal(text, MemberSignature.Format(m));
    }

    // --- Malformed / edge inputs: these pin the CURRENT (unvalidated) behaviour. ---
    // Item 6a of the code-review remediation plan will add structural validation and update these.

    [Fact]
    public void Parse_UnbalancedOpenParen_TreatedAsEmptyParamOperation()
    {
        ClassMember m = MemberSignature.Parse("foo(", MemberKind.Field);
        Assert.Equal(MemberKind.Operation, m.Kind);
        Assert.Equal("foo", m.Name);
        Assert.Equal(string.Empty, m.Parameters);
        Assert.Null(m.Type);
    }

    [Fact]
    public void Parse_StrayCloseParen_TreatedAsFieldName()
    {
        ClassMember m = MemberSignature.Parse("foo)", MemberKind.Field);
        Assert.Equal(MemberKind.Field, m.Kind);
        Assert.Equal("foo)", m.Name);
    }

    [Fact]
    public void Parse_MultipleColons_AbsorbsRemainderIntoType()
    {
        ClassMember m = MemberSignature.Parse("a: b: c", MemberKind.Field);
        Assert.Equal(MemberKind.Field, m.Kind);
        Assert.Equal("a", m.Name);
        Assert.Equal("b: c", m.Type);
    }

    [Fact]
    public void Parse_OperationWithEmptyTrailingType_YieldsNullType()
    {
        ClassMember m = MemberSignature.Parse("m(): ", MemberKind.Field);
        Assert.Equal(MemberKind.Operation, m.Kind);
        Assert.Equal("m", m.Name);
        Assert.Null(m.Type);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("+")]
    public void Parse_EmptyOrMarkerOnly_YieldsEmptyFieldName(string text)
    {
        ClassMember m = MemberSignature.Parse(text, MemberKind.Field);
        Assert.Equal(MemberKind.Field, m.Kind);
        Assert.Equal(string.Empty, m.Name);
    }
}
