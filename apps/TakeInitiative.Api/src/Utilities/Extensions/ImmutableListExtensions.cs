using System.Collections.Immutable;

namespace TakeInitiative.Utilities.Extensions;
public static class ImmutableListExtensions
{
	public static ImmutableList<T> ReplaceOrInsert<T>(this ImmutableList<T> list, Predicate<T> predicate, T item)
	{
		var matchingIndexes = list.Where(x => predicate(x)).Select((x, index) => index).ToList();
		if (matchingIndexes.Count == 0)
		{
			throw new ArgumentException("Replace or Insert: Predicate matched no elements.");
		}
		else if (matchingIndexes.Count > 1)
		{
			throw new ArgumentException("Replace or Insert: Predicate matched more than one element.");
		}

		var matchingIndex = matchingIndexes[0];
		return list.SetItem(matchingIndex, item);
	}
}