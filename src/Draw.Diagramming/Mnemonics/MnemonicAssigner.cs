using System.Collections.Generic;

namespace Draw.Diagramming.Mnemonics;

/// <summary>
/// Assigns a single mnemonic letter (a–z) to each name in a list, scoped to that list. Used by the
/// keyboard tool palette: one call per visible screen, so the category screen and each item screen get
/// independent letter spaces. Deterministic and order-stable — the same names always yield the same
/// letters — and collision-free for up to 26 entries.
/// </summary>
public static class MnemonicAssigner
{
    /// <summary>
    /// Returns one letter per name, in order. For each name the choice is: the first not-yet-used a–z
    /// letter that appears in the name (in reading order); failing that, the first unused a–z letter
    /// overall; failing that (more than 26 entries), <c>'?'</c>.
    /// </summary>
    public static char[] Assign(IReadOnlyList<string> names)
    {
        char[] assigned = new char[names.Count];
        bool[] used = new bool[26];

        for (int i = 0; i < names.Count; i++)
        {
            char pick = Pick(names[i], used);
            assigned[i] = pick == '\0' ? '?' : pick;
            if (pick != '\0')
            {
                used[pick - 'a'] = true;
            }
        }

        return assigned;
    }

    private static char Pick(string name, bool[] used)
    {
        // Prefer a letter from the name itself (first free one in reading order) — that keeps the
        // mnemonic recognisable, and the in-order scan also covers the "next free letter in the name"
        // fallback when the first letter is already taken.
        foreach (char raw in name)
        {
            char c = char.ToLowerInvariant(raw);
            if (c >= 'a' && c <= 'z' && !used[c - 'a'])
            {
                return c;
            }
        }

        // The name offered nothing free; take any unused letter so the entry is still reachable.
        for (char c = 'a'; c <= 'z'; c++)
        {
            if (!used[c - 'a'])
            {
                return c;
            }
        }

        return '\0';
    }
}
