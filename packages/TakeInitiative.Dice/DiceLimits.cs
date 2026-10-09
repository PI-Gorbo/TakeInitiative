namespace TakeInitiative.Dice;

/// <summary>
/// Upper bounds on what an expression may ask for. Roll expressions are user input
/// evaluated server-side, so these are a security requirement: they are enforced
/// before any dice are rolled.
/// </summary>
public static class DiceLimits
{
    public const int MaxDicePerPool = 1000;
    public const int MaxSides = 1000;
    public const int MaxTotalDice = 1000;
    public const int MaxNumber = 10000;
    public const int MaxExpressionLength = 200;
    public const int MaxNestingDepth = 32;
}
