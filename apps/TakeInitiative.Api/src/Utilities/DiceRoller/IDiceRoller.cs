using CSharpFunctionalExtensions;

namespace TakeInitiative.Utilities;
public interface IDiceRoller
{
    public Result<DiceRoll> EvaluateRoll(string roll);

    public DiceRoll RollD20();
}
