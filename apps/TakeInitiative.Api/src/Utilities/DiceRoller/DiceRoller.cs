using CSharpFunctionalExtensions;

namespace TakeInitiative.Utilities;

/// <summary>Adapts <see cref="DiceLanguage"/> to the API's railway types.</summary>
public class DiceRoller(Random random) : IDiceRoller
{
    public Result<DiceRoll> EvaluateRoll(string roll) =>
        DiceLanguage.Roll(roll, random).MapError(Describe);

    public DiceRoll RollD20() => DiceLanguage.RollD20(random);

    public Result Check(string roll) =>
        DiceLanguage.Check(roll).MapError(Describe);

    private static string Describe(DiceError[] errors) =>
        string.Join("; ", errors.Select(e => e.Message));
}
