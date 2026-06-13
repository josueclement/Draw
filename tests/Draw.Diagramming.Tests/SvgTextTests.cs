using Draw.Diagramming.Formatting;
using Xunit;

namespace Draw.Diagramming.Tests;

public class SvgTextTests
{
    [Theory]
    [InlineData("&", "&amp;")]
    [InlineData("<", "&lt;")]
    [InlineData(">", "&gt;")]
    [InlineData("\"", "&quot;")]
    [InlineData("'", "&apos;")]
    public void Escape_EncodesEachEntity(string input, string expected)
    {
        Assert.Equal(expected, SvgText.Escape(input));
    }

    [Fact]
    public void Escape_EncodesAllEntitiesInOrder()
    {
        Assert.Equal("&amp;&lt;&gt;&quot;&apos;", SvgText.Escape("&<>\"'"));
    }

    [Fact]
    public void Escape_EncodesAmpersandOnlyOnce()
    {
        // The single pass must not re-escape the '&' it just emitted (the classic chained-Replace bug).
        Assert.Equal("a &lt; b", SvgText.Escape("a < b"));
    }

    [Fact]
    public void Escape_ReturnsSameInstanceWhenNothingToEscape()
    {
        string input = "plain text 123";
        Assert.Same(input, SvgText.Escape(input));
    }

    [Fact]
    public void Escape_EmptyString()
    {
        Assert.Same("", SvgText.Escape(""));
    }

    [Theory]
    [InlineData(0d, "0")]
    [InlineData(12d, "12")]
    [InlineData(1.5, "1.5")]
    [InlineData(1.2342, "1.234")]
    [InlineData(1.2348, "1.235")]
    [InlineData(-3.14159, "-3.142")]
    public void Num_FormatsWithInvariantCultureAndThreeDecimals(double value, string expected)
    {
        Assert.Equal(expected, SvgText.Num(value));
    }
}
