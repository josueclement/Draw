using System.Collections.Generic;
using Draw.Diagramming.Mnemonics;
using Xunit;

namespace Draw.Diagramming.Tests;

public class MnemonicAssignerTests
{
    [Fact]
    public void Assign_PrefersFirstLetter_ThenNextFreeLetterInName()
    {
        // "Rounded rectangle" can't take 'r' (Rectangle has it), so it falls to the next free in-name letter 'o'.
        char[] letters = MnemonicAssigner.Assign(new[] { "Rectangle", "Rounded rectangle", "Ellipse" });

        Assert.Equal(new[] { 'r', 'o', 'e' }, letters);
    }

    [Fact]
    public void Assign_IsCaseInsensitive_AndLowercases()
    {
        char[] letters = MnemonicAssigner.Assign(new[] { "ZEBRA" });

        Assert.Equal(new[] { 'z' }, letters);
    }

    [Fact]
    public void Assign_CommonShapes_ProducesFifteenDistinctLetters()
    {
        // The hardest real screen: 15 entries, many sharing leading letters (R/R, P/P, C/C/C/C).
        string[] common =
        {
            "Rectangle", "Rounded rectangle", "Ellipse", "Circle", "Diamond",
            "Parallelogram", "Trapezoid", "Triangle", "Hexagon", "Pentagon",
            "Octagon", "Star", "Cross", "Cloud", "Callout",
        };

        char[] letters = MnemonicAssigner.Assign(common);

        Assert.Equal(common.Length, letters.Length);
        Assert.DoesNotContain('?', letters);
        Assert.Equal(common.Length, new HashSet<char>(letters).Count);
    }

    [Fact]
    public void Assign_IsDeterministic_ForTheSameInput()
    {
        string[] names = { "Aggregation", "Composition", "Generalization", "Association" };

        Assert.Equal(MnemonicAssigner.Assign(names), MnemonicAssigner.Assign(names));
    }

    [Fact]
    public void Assign_IsScopedPerCall_NoCarryOverBetweenScreens()
    {
        // Two independent "screens": each should be free to reuse 'a' — there is no shared state.
        char[] first = MnemonicAssigner.Assign(new[] { "Apple" });
        char[] second = MnemonicAssigner.Assign(new[] { "Avocado" });

        Assert.Equal('a', first[0]);
        Assert.Equal('a', second[0]);
    }

    [Fact]
    public void Assign_NameWithoutLetters_FallsBackToFirstUnusedLetter()
    {
        char[] letters = MnemonicAssigner.Assign(new[] { "123", "456" });

        Assert.Equal(new[] { 'a', 'b' }, letters);
    }

    [Fact]
    public void Assign_MoreThanTwentySixEntries_ExhaustsTheAlphabetThenYieldsQuestionMark()
    {
        // 27 identical full-alphabet names: the first 26 consume a–z, the 27th has nothing left.
        List<string> names = new();
        for (int i = 0; i < 27; i++)
        {
            names.Add("abcdefghijklmnopqrstuvwxyz");
        }

        char[] letters = MnemonicAssigner.Assign(names);

        Assert.Equal('a', letters[0]);
        Assert.Equal('z', letters[25]);
        Assert.Equal('?', letters[26]);
    }
}
