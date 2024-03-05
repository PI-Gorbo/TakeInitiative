namespace TakeInitiative.Api.Models;

public record CombatNpc : Npc, ICombatCharacter
{
	public int InitiativeValue { get; init; }
	public bool Hidden { get; init; }
}
