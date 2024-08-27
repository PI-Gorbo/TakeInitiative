using System.Diagnostics.CodeAnalysis;

namespace TakeInitiative.Utilities;

public class InitiativeComparer : IComparer<int[]>, IEqualityComparer<int[]>
{
    public int Compare(int[]? x, int[]? y)
    {
        if (x == null && y == null)
        {
            return 0;
        }

        if (x == null & y != null)
        {
            return 1;
        }

        if (y == null && x != null)
        {
            return -1;
        }

        var length = Math.Min(x!.Length, y!.Length);
        var currentIndex = 0;
        while (x[currentIndex] == y[currentIndex] && currentIndex != length - 1)
        {
            currentIndex++;
        }

        return x[currentIndex] > y[currentIndex] ? 1 : -1;
    }

    public bool Equals(int[]? x, int[]? y)
    {
        if (x == y) // Reference equals or null.
        {
            return true;
        }

        if ((x == null && y != null) || (x != null && y == null))
        {
            return false;
        }

        return x!.SequenceEqual(y!);
    }

    public int GetHashCode([DisallowNull] int[] obj)
    {
        return obj.Aggregate(0, (a, v) =>
            HashCode.Combine(a, v.GetHashCode()));
    }
}




