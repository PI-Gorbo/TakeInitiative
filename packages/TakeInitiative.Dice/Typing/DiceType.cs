namespace TakeInitiative.Dice.Typing;

public abstract record DiceType
{
    /// <summary>A collection of dice that can still be selected from.</summary>
    public sealed record Pool(int Count, int Sides) : DiceType
    {
        public override string ToString() => $"Pool({Count}, {Sides})";
    }

    /// <summary>A single value: a constant, a sum, a product, a min/max.</summary>
    public sealed record Scalar : DiceType
    {
        public override string ToString() => "Scalar";
    }

    public static readonly Scalar ScalarType = new();
}
