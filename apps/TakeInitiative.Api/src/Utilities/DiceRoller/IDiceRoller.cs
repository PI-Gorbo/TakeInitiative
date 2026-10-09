using CSharpFunctionalExtensions;

namespace TakeInitiative.Utilities;
public interface IDiceRoller
{
    public Result<DiceRoll> EvaluateRoll(string roll);

    public DiceRoll RollD20();

    /// <summary>
    /// Validates an expression without rolling it. The failure is a message written
    /// for the DM, ready for a FluentValidation failure.
    /// </summary>
    public Result Check(string roll);
}
