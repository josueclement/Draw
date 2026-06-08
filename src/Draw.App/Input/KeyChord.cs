using System;
using System.Collections.Generic;
using Avalonia.Input;

namespace Draw.App.Input;

/// <summary>A single key press: a key plus the modifiers held with it.</summary>
public readonly record struct KeyStroke(Key Key, KeyModifiers Modifiers);

/// <summary>A parsed keymap binding: an ordered sequence of <see cref="KeyStroke"/>s and its action id.</summary>
public sealed record ParsedBinding(IReadOnlyList<KeyStroke> Strokes, string Action);

/// <summary>Order-sensitive structural equality for keystroke sequences, used as dictionary/set keys.</summary>
public sealed class KeyStrokeSequenceComparer : IEqualityComparer<IReadOnlyList<KeyStroke>>
{
    public static readonly KeyStrokeSequenceComparer Instance = new();

    public bool Equals(IReadOnlyList<KeyStroke>? x, IReadOnlyList<KeyStroke>? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null || x.Count != y.Count)
        {
            return false;
        }

        for (int i = 0; i < x.Count; i++)
        {
            if (!x[i].Equals(y[i]))
            {
                return false;
            }
        }

        return true;
    }

    public int GetHashCode(IReadOnlyList<KeyStroke> obj)
    {
        HashCode hash = new();
        foreach (KeyStroke stroke in obj)
        {
            hash.Add(stroke);
        }

        return hash.ToHashCode();
    }
}
