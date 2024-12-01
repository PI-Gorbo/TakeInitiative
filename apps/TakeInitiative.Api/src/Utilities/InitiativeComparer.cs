using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace TakeInitiative.Utilities;

public class InitiativeComparer : IComparer<DiceRoll[]>, IEqualityComparer<DiceRoll[]>
{

    public int Compare(DiceRoll[]? x, DiceRoll[]? y)
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

        return x[currentIndex].Total > y[currentIndex].Total ? 1 : -1;
    }

    public bool Equals(DiceRoll[]? x, DiceRoll[]? y)
    {
        if (x == y) // Reference equals or null.
        {
            return true;
        }

        if ((x == null && y != null) || (x != null && y == null))
        {
            return false;
        }

        return x!.Select(x => x.Total).SequenceEqual(y!.Select(x => x.Total));
    }

    public int GetHashCode([DisallowNull] DiceRoll[] obj)
    {
        return ((IStructuralEquatable)obj.Select(x => x.Total).ToArray()).GetHashCode(EqualityComparer<int>.Default);
    }
}




