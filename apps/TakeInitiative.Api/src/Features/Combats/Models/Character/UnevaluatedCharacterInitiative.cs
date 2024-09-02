using CSharpFunctionalExtensions;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features;

public record UnevaluatedCharacterInitiative(string Roll)
{
    public Result<DiceRoll> RollInitiative(IDiceRoller roller)
    {
        return Result.Success<string?>(Roll)
            .EnsureNotNull("No roll value provided")
            .Bind(roller.EvaluateRoll);
    }
}

