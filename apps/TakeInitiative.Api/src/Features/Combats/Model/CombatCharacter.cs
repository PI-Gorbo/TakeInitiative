using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;
using FluentValidation;

namespace TakeInitiative.Api.Models;
public record CombatCharacter : Character
{
	public required Guid PlayerId { get; init; }
    public required Guid? PlannedCharacterId {get; init;}
	public required int[] InitiativeValue { get; init; }
	public required bool Hidden { get; init; }
    public required int? CopyNumber {get; set;}
}

public class CombatCharacterValidator : CharacterValidator<CombatCharacter>
{
	public CombatCharacterValidator()
	{
		RuleFor(x => x.PlayerId)
			.NotEmpty();
	}
}
