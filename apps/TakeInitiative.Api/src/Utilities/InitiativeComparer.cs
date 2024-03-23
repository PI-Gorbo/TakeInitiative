namespace TakeInitiative.Utilities;

public class InitiativeComparer : IComparer<int[]>
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

        var length = Math.Min(x.Length, y.Length);
        var currentIndex = 0;
        while (x[currentIndex] == y[currentIndex] && currentIndex != length - 1)
        {
            currentIndex++;
        }

        return x[currentIndex] > y[currentIndex] ? 1 : -1;
    }
}




