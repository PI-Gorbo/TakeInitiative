using FluentValidation;

namespace TakeInitiative.Api.Models;

public record CombatPlayerCharacter : CombatCharacter
{
	public required Guid PlayerId { get; set; }
}

public class CombatPlayerCharacterValidator : BaseCombatCharacterValidator<CombatPlayerCharacter>
{

}
