using System;
using System.Globalization;

namespace Draw.Model.Styling;

/// <summary>
/// A framework-agnostic 32-bit ARGB color. Serialized as a <c>#AARRGGBB</c> hex string.
/// </summary>
public readonly record struct ArgbColor(byte A, byte R, byte G, byte B)
{
    public static ArgbColor Transparent => new(0, 0, 0, 0);
    public static ArgbColor Black => new(255, 0, 0, 0);
    public static ArgbColor White => new(255, 255, 255, 255);

    public static ArgbColor FromRgb(byte r, byte g, byte b) => new(255, r, g, b);

    public ArgbColor WithAlpha(byte a) => this with { A = a };

    public string ToHex() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";

    public override string ToString() => ToHex();

    public static ArgbColor Parse(string hex)
    {
        if (TryParse(hex, out ArgbColor color))
        {
            return color;
        }

        throw new FormatException($"'{hex}' is not a valid #RRGGBB or #AARRGGBB color.");
    }

    public static bool TryParse(string? hex, out ArgbColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        ReadOnlySpan<char> span = hex.AsSpan().Trim();
        if (span.Length > 0 && span[0] == '#')
        {
            span = span[1..];
        }

        // RRGGBB -> assume opaque.
        if (span.Length == 6)
        {
            if (TryByte(span.Slice(0, 2), out byte r6)
                && TryByte(span.Slice(2, 2), out byte g6)
                && TryByte(span.Slice(4, 2), out byte b6))
            {
                color = new ArgbColor(255, r6, g6, b6);
                return true;
            }

            return false;
        }

        // AARRGGBB.
        if (span.Length == 8)
        {
            if (TryByte(span.Slice(0, 2), out byte a)
                && TryByte(span.Slice(2, 2), out byte r)
                && TryByte(span.Slice(4, 2), out byte g)
                && TryByte(span.Slice(6, 2), out byte b))
            {
                color = new ArgbColor(a, r, g, b);
                return true;
            }
        }

        return false;
    }

    private static bool TryByte(ReadOnlySpan<char> span, out byte value)
        => byte.TryParse(span, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
}
