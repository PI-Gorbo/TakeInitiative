using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;
using FluentValidation;

namespace TakeInitiative.Api.Models;
public record CombatCharacter : Character
{
	public required Guid PlayerId { get; set; }
	public required int[] InitiativeValue { get; init; }
	public required bool Hidden { get; init; }
    public required int? QuantityNumber {get; set;}
}

public class CombatCharacterValidator : CharacterValidator<CombatCharacter>
{
	public CombatCharacterValidator()
	{
		RuleFor(x => x.PlayerId)
			.NotEmpty();
	}
}
