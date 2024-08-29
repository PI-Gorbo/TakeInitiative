using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using TakeInitiative.Api.Features;

namespace TakeInitiative.Utilities;
public interface IHealthRoller
{
    public Result<Dictionary<Guid, CharacterHealth>> ComputeRolls(List<StagedCharacter> characters);
}

public class HealthRoller(IDiceRoller roller) : IHealthRoller
{
    public Result<Dictionary<Guid, CharacterHealth>> ComputeRolls(List<StagedCharacter> characters)
    {
        return characters
            .Select(x =>
            {
                Result<CharacterHealth> evaluatedHealth = x.Health switch
                {
                    UnevaluatedCharacterHealth.None => Result.Success<CharacterHealth>(new CharacterHealth.None()),
                    UnevaluatedCharacterHealth.Fixed @fixed => Result.Success<CharacterHealth>(new CharacterHealth.Fixed(@fixed.CurrentHealth, @fixed.MaxHealth)),
                    UnevaluatedCharacterHealth.Roll roll => Result.Success(roll.RollString)
                        .EnsureNotNull("RollString was null.")
                        .Bind(roller.EvaluateRoll)
                        .Map(rolledHealth => (CharacterHealth)new CharacterHealth.Fixed(rolledHealth, rolledHealth)),
                    _ => throw new InvalidOperationException("Invalid Operation")
                };

                return new
                {
                    x.Id,
                    Health = evaluatedHealth
                };
            })
            .Aggregate(seed: Result.Success<Dictionary<Guid, CharacterHealth>>([]), (dict, roll) =>
            {
                if (dict.IsFailure) return dict;

                if (roll.Health.IsFailure)
                {
                    return roll.Health.ConvertFailure<Dictionary<Guid, CharacterHealth>>();
                }

                dict.Value.Add(roll.Id, roll.Health.Value);
                return dict;
            });
    }
}