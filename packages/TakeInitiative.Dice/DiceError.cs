namespace TakeInitiative.Dice;

/// <summary>
/// A problem with a roll expression. <see cref="Position"/> and <see cref="Length"/>
/// locate the offending span in the source so a UI can underline it.
/// </summary>
/// <remarks>
/// <see cref="Code"/> is the stable contract; <see cref="Message"/> is written for a
/// DM mid-combat and may be reworded at any time.
/// </remarks>
public sealed record DiceError(string Code, string Message, int Position, int Length);

public static class DiceErrorCodes
{
    public const string ParseError = "PARSE_ERROR";
    public const string LegacyKeepSyntax = "LEGACY_KEEP_SYNTAX";

    public const string AdvRequiresD20 = "ADV_REQUIRES_D20";
    public const string KeepRequiresPool = "KEEP_REQUIRES_POOL";
    public const string KeepExceedsPool = "KEEP_EXCEEDS_POOL";
    public const string KeepAtLeastOne = "KEEP_AT_LEAST_ONE";
    public const string DiceCountZero = "DICE_COUNT_ZERO";
    public const string DiceSidesZero = "DICE_SIDES_ZERO";
    public const string DivideByZero = "DIVIDE_BY_ZERO";
    public const string ResultOutOfRange = "RESULT_OUT_OF_RANGE";

    public const string PoolTooLarge = "POOL_TOO_LARGE";
    public const string TooManySides = "TOO_MANY_SIDES";
    public const string TooManyDice = "TOO_MANY_DICE";
    public const string NumberTooLarge = "NUMBER_TOO_LARGE";
    public const string ExpressionTooLong = "EXPRESSION_TOO_LONG";
    public const string ExpressionTooDeep = "EXPRESSION_TOO_DEEP";
}
