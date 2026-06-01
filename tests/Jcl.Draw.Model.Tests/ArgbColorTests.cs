using Jcl.Draw.Model.Styling;
using Xunit;

namespace Jcl.Draw.Model.Tests;

public class ArgbColorTests
{
    [Theory]
    [InlineData("#FF112233", 0xFF, 0x11, 0x22, 0x33)]
    [InlineData("#112233", 0xFF, 0x11, 0x22, 0x33)]
    [InlineData("80ffeedd", 0x80, 0xFF, 0xEE, 0xDD)]
    public void TryParse_ValidHex_ParsesComponents(string hex, byte a, byte r, byte g, byte b)
    {
        bool ok = ArgbColor.TryParse(hex, out ArgbColor color);

        Assert.True(ok);
        Assert.Equal(new ArgbColor(a, r, g, b), color);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("xyz")]
    [InlineData("#12")]
    [InlineData("#1234567")]
    public void TryParse_InvalidHex_ReturnsFalse(string? hex)
    {
        Assert.False(ArgbColor.TryParse(hex, out _));
    }

    [Fact]
    public void ToHex_RoundTripsThroughParse()
    {
        ArgbColor original = new(0x80, 0x10, 0x20, 0x30);

        ArgbColor parsed = ArgbColor.Parse(original.ToHex());

        Assert.Equal(original, parsed);
    }
}
