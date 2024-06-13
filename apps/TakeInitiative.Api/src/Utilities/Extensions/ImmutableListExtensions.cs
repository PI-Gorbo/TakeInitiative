using System.Collections.Immutable;

namespace TakeInitiative.Utilities.Extensions;
public static class ImmutableListExtensions
{
    public static ImmutableList<T> ReplaceOrInsertIfExists<T>(this ImmutableList<T> list, Predicate<T> predicate, T item)
    {
        var matchingIndexes = list.Select((item, index) => (item, index)).Where((x) => predicate(x.item)).ToList();
        if (matchingIndexes.Count == 0)
        {
            return list;
        }

        else if (matchingIndexes.Count > 1)
        {
            throw new ArgumentException("Replace or Insert: Predicate matched more than one element.");
        }

        var matchingIndex = matchingIndexes[0].index;
        return list.SetItem(matchingIndex, item);
    }
}