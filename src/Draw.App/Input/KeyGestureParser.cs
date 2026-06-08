using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;

namespace Draw.App.Input;

/// <summary>
/// Parses keymap "keys" strings into <see cref="KeyStroke"/> sequences and renders them back for display.
/// A single gesture such as "Ctrl+Shift+S" yields one stroke; a chord such as "a s r" yields one stroke
/// per space-separated token. Within a token, "+"-separated words are modifiers except the last, which
/// is the key.
/// </summary>
public static class KeyGestureParser
{
    /// <summary>Parses <paramref name="keys"/>; returns null if any token is unrecognised.</summary>
    public static IReadOnlyList<KeyStroke>? Parse(string? keys)
    {
        if (string.IsNullOrWhiteSpace(keys))
        {
            return null;
        }

        string[] tokens = keys.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return null;
        }

        List<KeyStroke> strokes = new(tokens.Length);
        foreach (string token in tokens)
        {
            if (!TryParseStroke(token, out KeyStroke stroke))
            {
                return null;
            }

            strokes.Add(stroke);
        }

        return strokes;
    }

    /// <summary>Renders a keystroke sequence for the status bar, e.g. "a s" or "Ctrl+Shift+S".</summary>
    public static string Describe(IReadOnlyList<KeyStroke> strokes) => string.Join(" ", strokes.Select(Describe));

    /// <summary>Renders a single keystroke, e.g. "a" or "Ctrl+Shift+L".</summary>
    public static string Describe(KeyStroke stroke)
    {
        List<string> parts = new();
        if (stroke.Modifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (stroke.Modifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (stroke.Modifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (stroke.Modifiers.HasFlag(KeyModifiers.Meta))
        {
            parts.Add("Meta");
        }

        parts.Add(DescribeKey(stroke.Key));
        return string.Join("+", parts);
    }

    private static bool TryParseStroke(string token, out KeyStroke stroke)
    {
        stroke = default;
        string[] parts = token.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        KeyModifiers modifiers = KeyModifiers.None;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (!TryParseModifier(parts[i], out KeyModifiers modifier))
            {
                return false;
            }

            modifiers |= modifier;
        }

        if (!TryParseKey(parts[^1], out Key key))
        {
            return false;
        }

        stroke = new KeyStroke(key, modifiers);
        return true;
    }

    private static bool TryParseModifier(string word, out KeyModifiers modifier)
    {
        switch (word.ToLowerInvariant())
        {
            case "ctrl":
            case "control":
                modifier = KeyModifiers.Control;
                return true;
            case "shift":
                modifier = KeyModifiers.Shift;
                return true;
            case "alt":
                modifier = KeyModifiers.Alt;
                return true;
            case "meta":
            case "cmd":
            case "win":
            case "super":
                modifier = KeyModifiers.Meta;
                return true;
            default:
                modifier = KeyModifiers.None;
                return false;
        }
    }

    private static bool TryParseKey(string word, out Key key)
    {
        // Top-row digits are named D0..D9 in the Key enum, so "1" must map to D1, not the integer value 1.
        if (word.Length == 1 && word[0] >= '0' && word[0] <= '9')
        {
            key = Key.D0 + (word[0] - '0');
            return true;
        }

        return Enum.TryParse(word, ignoreCase: true, out key) && key != Key.None;
    }

    private static string DescribeKey(Key key)
    {
        if (key >= Key.A && key <= Key.Z)
        {
            return ((char)('a' + (key - Key.A))).ToString();
        }

        if (key >= Key.D0 && key <= Key.D9)
        {
            return ((char)('0' + (key - Key.D0))).ToString();
        }

        return key.ToString();
    }
}
