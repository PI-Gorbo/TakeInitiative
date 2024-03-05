namespace TakeInitiative.Api.Models;

public record CombatPlayerCharacter : PlayerCharacter, ICombatCharacter
{
	public int InitiativeValue { get; init; }
	public bool Hidden { get; init; }
}
