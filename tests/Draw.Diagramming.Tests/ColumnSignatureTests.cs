using Draw.Diagramming.Er;
using Draw.Model.Nodes;
using Xunit;

namespace Draw.Diagramming.Tests;

public class ColumnSignatureTests
{
    [Fact]
    public void Format_NameOnly_WhenNoType()
    {
        EntityColumn c = new() { Name = "note" };
        Assert.Equal("note", ColumnSignature.Format(c));
    }

    [Fact]
    public void Format_NameAndType()
    {
        EntityColumn c = new() { Name = "title", Type = "text" };
        Assert.Equal("title: text", ColumnSignature.Format(c));
    }

    [Fact]
    public void Format_PrimaryKey_SuppressesNotNull()
    {
        EntityColumn c = new() { Name = "id", Type = "int", IsPrimaryKey = true };
        Assert.Equal("id: int PK", ColumnSignature.Format(c));
    }

    [Fact]
    public void Format_ForeignKey()
    {
        EntityColumn c = new() { Name = "user_id", Type = "int", IsForeignKey = true };
        Assert.Equal("user_id: int FK", ColumnSignature.Format(c));
    }

    [Fact]
    public void Format_UniqueNotNull()
    {
        EntityColumn c = new() { Name = "email", Type = "varchar(255)", IsUnique = true, IsNullable = false };
        Assert.Equal("email: varchar(255) UNIQUE NOT NULL", ColumnSignature.Format(c));
    }

    [Fact]
    public void Format_FlagOrder_IsPkFkUnique()
    {
        // NOT NULL is suppressed because the column is a primary key.
        EntityColumn c = new()
        {
            Name = "x", Type = "int",
            IsPrimaryKey = true, IsForeignKey = true, IsUnique = true, IsNullable = false,
        };
        Assert.Equal("x: int PK FK UNIQUE", ColumnSignature.Format(c));
    }

    [Fact]
    public void Parse_PrimaryKey_IsImplicitlyNotNullable()
    {
        EntityColumn c = ColumnSignature.Parse("id: int PK");
        Assert.Equal("id", c.Name);
        Assert.Equal("int", c.Type);
        Assert.True(c.IsPrimaryKey);
        Assert.False(c.IsNullable);
    }

    [Fact]
    public void Parse_UniqueNotNull()
    {
        EntityColumn c = ColumnSignature.Parse("email: varchar(255) UNIQUE NOT NULL");
        Assert.Equal("email", c.Name);
        Assert.Equal("varchar(255)", c.Type);
        Assert.True(c.IsUnique);
        Assert.False(c.IsNullable);
    }

    [Fact]
    public void Parse_PlainColumn_DefaultsToNullable()
    {
        EntityColumn c = ColumnSignature.Parse("name: text");
        Assert.Equal("text", c.Type);
        Assert.True(c.IsNullable);
        Assert.False(c.IsPrimaryKey);
    }

    [Theory]
    [InlineData("count: int NN", false)]
    [InlineData("count: int NOT NULL", false)]
    [InlineData("count: int NULL", true)]
    [InlineData("count: int", true)]
    public void Parse_NullabilityFlags(string text, bool expectedNullable)
    {
        EntityColumn c = ColumnSignature.Parse(text);
        Assert.Equal(expectedNullable, c.IsNullable);
    }

    [Fact]
    public void Parse_ForeignKey()
    {
        EntityColumn c = ColumnSignature.Parse("user_id: int FK");
        Assert.True(c.IsForeignKey);
        Assert.Equal("int", c.Type);
    }

    [Fact]
    public void Parse_Flags_AreCaseInsensitive()
    {
        EntityColumn c = ColumnSignature.Parse("id: int pk");
        Assert.True(c.IsPrimaryKey);
    }

    [Theory]
    [InlineData("price: double precision", "double precision")]
    [InlineData("amount: numeric NOT NULL", "numeric")]
    public void Parse_RetainsMultiWordType(string text, string expectedType)
    {
        EntityColumn c = ColumnSignature.Parse(text);
        Assert.Equal(expectedType, c.Type);
    }

    [Fact]
    public void Parse_NoColonForm_TreatsLeadingTextAsName()
    {
        EntityColumn c = ColumnSignature.Parse("id PK");
        Assert.Equal("id", c.Name);
        Assert.Null(c.Type);
        Assert.True(c.IsPrimaryKey);
    }

    [Theory]
    [InlineData("id: int PK")]
    [InlineData("email: varchar(255) UNIQUE NOT NULL")]
    [InlineData("name: text")]
    [InlineData("user_id: int FK")]
    public void Parse_RoundTripsWithFormat(string text)
    {
        EntityColumn c = ColumnSignature.Parse(text);
        Assert.Equal(text, ColumnSignature.Format(c));
    }

    // --- Parse is intentionally total: it never throws and always returns a best-effort column,
    // even for malformed input (the cases below document that leniency). TryParse (further down) is
    // the strict gate that rejects the same inputs so the editor can refuse a bad commit. ---

    [Fact]
    public void Parse_TypeCollidingWithFlagToken_IsConsumedAsFlag()
    {
        EntityColumn c = ColumnSignature.Parse("flag: unique");
        Assert.Equal("flag", c.Name);
        Assert.Null(c.Type);
        Assert.True(c.IsUnique);
    }

    [Fact]
    public void Parse_ContradictoryNullFlags_LastApplicationWins()
    {
        EntityColumn c = ColumnSignature.Parse("x: int NOT NULL NULL");
        Assert.Equal("int", c.Type);
        Assert.False(c.IsNullable);
    }

    [Fact]
    public void Parse_Empty_YieldsEmptyNullableColumn()
    {
        EntityColumn c = ColumnSignature.Parse("");
        Assert.Equal(string.Empty, c.Name);
        Assert.Null(c.Type);
        Assert.True(c.IsNullable);
    }

    // --- TryParse: strict structural validation (code-review remediation item 6a). ---

    [Theory]
    [InlineData("id: int PK")]
    [InlineData("email: varchar(255) UNIQUE NOT NULL")]
    [InlineData("name: text")]
    [InlineData("user_id: int FK")]
    [InlineData("price: double precision")]
    [InlineData("count: int NULL")]
    [InlineData("id PK")]
    public void TryParse_WellFormed_SucceedsAndMatchesParse(string text)
    {
        bool ok = ColumnSignature.TryParse(text, out EntityColumn? parsed, out string? error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(parsed);
        // The accepted result is identical to the lenient Parse for well-formed input.
        Assert.Equal(ColumnSignature.Format(ColumnSignature.Parse(text)), ColumnSignature.Format(parsed));
    }

    [Theory]
    [InlineData("")]                       // empty
    [InlineData("   ")]                     // whitespace only
    [InlineData("PK")]                      // a lone flag -> no name
    [InlineData("a: b: c")]                 // more than one ':' separator
    [InlineData("id:")]                     // ':' with no type
    [InlineData("id: PK")]                  // ':' followed only by a flag -> no type left
    [InlineData("flag: unique")]            // type word collides with a flag -> no type left
    [InlineData("x: int NOT NULL NULL")]    // contradictory NULL / NOT NULL
    public void TryParse_Malformed_FailsWithError(string text)
    {
        bool ok = ColumnSignature.TryParse(text, out EntityColumn? parsed, out string? error);

        Assert.False(ok);
        Assert.Null(parsed);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
