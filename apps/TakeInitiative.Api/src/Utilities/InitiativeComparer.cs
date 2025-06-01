namespace TakeInitiative.Utilities;

public class InitiativeComparer : IComparer<int[]>, IEqualityComparer<int[]>, IComparer<DiceRoll[]>,
    IEqualityComparer<DiceRoll[]>
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

    public int GetHashCode(int[] obj)
    {
        return obj.GetHashCode();
    }

    public int Compare(DiceRoll[]? x, DiceRoll[]? y) =>
        this.Compare(x?.Select(x => x.Total).ToArray(), y?.Select(diceRoll => diceRoll.Total).ToArray());

    public bool Equals(DiceRoll[]? x, DiceRoll[]? y) =>
        this.Equals(x?.Select(x => x.Total).ToArray(), y?.Select(diceRoll => diceRoll.Total).ToArray());
    public int GetHashCode(DiceRoll[] obj) => this.GetHashCode(obj?.Select(x => x.Total).ToArray()!);
}