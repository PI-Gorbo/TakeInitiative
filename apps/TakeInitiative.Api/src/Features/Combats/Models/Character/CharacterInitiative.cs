using CSharpFunctionalExtensions;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features;

public record CharacterInitiative
{
    public required InitiativeStrategy Strategy { get; set; }

    public required string Value { get; set; }

    public int? Fixed
    {
        get
        {
            if (Strategy != InitiativeStrategy.Fixed) return null;

            try
            {
                return Convert.ToInt32(Value);
            }
            catch
            {
                return 0;
            }
        }
    }

    public string? Roll
    {
        get
        {
            if (Strategy != InitiativeStrategy.Roll) return null;
            return Value;
        }
    }

    public Result<int> RollInitiative(IDiceRoller roller)
    {
        if (this.Strategy == InitiativeStrategy.Fixed)
        {
            return Result.Success(Fixed)
                .Ensure(x => x.HasValue, "No fixed value provided.")
                .Map(x => x!.Value);
        }

        return Result.Success(Roll)
            .EnsureNotNull("No roll value provided")
            .Bind(roller.EvaluateRoll);
    }
}
