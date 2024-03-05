namespace TakeInitiative.Api.Models;

public record AddNpcsEvent
{
	public required Guid UserId { get; init; }
	public required CombatOpenedNpc Npcs { get; init; }
};
