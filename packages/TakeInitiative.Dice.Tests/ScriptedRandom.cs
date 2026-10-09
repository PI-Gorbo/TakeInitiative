namespace TakeInitiative.Dice.Tests;

/// <summary>Returns the given die results in order, so evaluations are exact.</summary>
public sealed class ScriptedRandom(params int[] rolls) : Random
{
    private readonly Queue<int> _rolls = new(rolls);

    public override int Next(int minValue, int maxValue)
    {
        if (!_rolls.TryDequeue(out var roll))
        {
            throw new InvalidOperationException("ScriptedRandom ran out of rolls");
        }

        if (roll < minValue || roll >= maxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(roll), roll, $"scripted roll outside [{minValue}, {maxValue})");
        }

        return roll;
    }

    public int Remaining => _rolls.Count;
}
