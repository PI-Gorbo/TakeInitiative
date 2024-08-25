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
        return Fixed
            ?? roller.EvaluateRoll(Roll);
    }
}
