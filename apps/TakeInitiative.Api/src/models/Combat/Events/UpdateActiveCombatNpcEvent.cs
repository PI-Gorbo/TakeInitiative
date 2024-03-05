namespace TakeInitiative.Api.Models;

public record UpdateActiveCombatNpcEvent
{
	public required Guid UserId { get; init; }
	public required CombatPlayerCharacter Npc { get; init; }
};
