using System;
using System.Collections.Generic;
using System.Linq;

namespace Draw.Diagramming.Layout;

/// <summary>The four stacking-order moves a user can apply to the selected shapes.</summary>
public enum ZOrderOperation
{
    BringToFront,
    BringForward,
    SendBackward,
    SendToBack,
}

/// <summary>
/// Pure stacking-order reordering. UI-agnostic: the caller passes the items in back-to-front order
/// (index 0 = backmost) and a predicate marking the selected ones, and gets back the items in their
/// new back-to-front order. Selected items keep their relative order; "forward"/"backward" move one
/// level with a contiguous selected block travelling as a single unit. A no-op (returns the input
/// order) when fewer than two items are supplied or nothing — or everything — is selected.
/// </summary>
public static class ZOrderArranger
{
    public static IReadOnlyList<T> Reorder<T>(IReadOnlyList<T> ordered, Func<T, bool> isSelected, ZOrderOperation operation)
    {
        ArgumentNullException.ThrowIfNull(ordered);
        ArgumentNullException.ThrowIfNull(isSelected);

        int n = ordered.Count;
        T[] items = ordered.ToArray();
        bool[] selected = new bool[n];
        int selectedCount = 0;
        for (int i = 0; i < n; i++)
        {
            selected[i] = isSelected(items[i]);
            if (selected[i])
            {
                selectedCount++;
            }
        }

        // Nothing can change: too few items, none selected, or all selected (already grouped together).
        if (n < 2 || selectedCount == 0 || selectedCount == n)
        {
            return items;
        }

        switch (operation)
        {
            case ZOrderOperation.BringToFront:
                // Non-selected stay at the back; selected move to the front, both keeping their order.
                return items.Where((_, i) => !selected[i])
                    .Concat(items.Where((_, i) => selected[i]))
                    .ToArray();

            case ZOrderOperation.SendToBack:
                return items.Where((_, i) => selected[i])
                    .Concat(items.Where((_, i) => !selected[i]))
                    .ToArray();

            case ZOrderOperation.BringForward:
                // Each selected item swaps one slot toward the front. Scanning front→back means a
                // contiguous block only has its frontmost element cross the gap, so the block moves
                // up by exactly one level as a unit.
                for (int i = n - 2; i >= 0; i--)
                {
                    if (selected[i] && !selected[i + 1])
                    {
                        (items[i], items[i + 1]) = (items[i + 1], items[i]);
                        (selected[i], selected[i + 1]) = (selected[i + 1], selected[i]);
                    }
                }

                return items;

            case ZOrderOperation.SendBackward:
                for (int i = 1; i < n; i++)
                {
                    if (selected[i] && !selected[i - 1])
                    {
                        (items[i], items[i - 1]) = (items[i - 1], items[i]);
                        (selected[i], selected[i - 1]) = (selected[i - 1], selected[i]);
                    }
                }

                return items;

            default:
                return items;
        }
    }

    /// <summary>
    /// Reorders <paramref name="items"/> in two independent stacking bands so a lower-band item can never
    /// cross above an upper-band one. Items are split by <paramref name="isLowerBand"/>; each band is taken
    /// back-to-front by <paramref name="zIndex"/>, reordered with <see cref="Reorder{T}"/>, and the lower
    /// band is concatenated before the upper. Returns the full back-to-front order. (Used for the
    /// "system boundaries always behind shapes" rule.)
    /// </summary>
    public static IReadOnlyList<T> ReorderInBands<T>(
        IReadOnlyList<T> items,
        Func<T, bool> isLowerBand,
        Func<T, bool> isSelected,
        Func<T, int> zIndex,
        ZOrderOperation operation)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(isLowerBand);
        ArgumentNullException.ThrowIfNull(zIndex);

        List<T> lower = items.Where(i => isLowerBand(i)).OrderBy(zIndex).ToList();
        List<T> upper = items.Where(i => !isLowerBand(i)).OrderBy(zIndex).ToList();

        List<T> result = new(lower.Count + upper.Count);
        result.AddRange(Reorder(lower, isSelected, operation));
        result.AddRange(Reorder(upper, isSelected, operation));
        return result;
    }
}
