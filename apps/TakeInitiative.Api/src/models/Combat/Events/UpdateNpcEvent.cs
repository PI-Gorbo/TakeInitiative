namespace TakeInitiative.Api.Models;

public record UpdateNpcEvent
{
	public required Guid UserId { get; init; }
	public required CombatOpenedNpc Npc { get; init; }
};
