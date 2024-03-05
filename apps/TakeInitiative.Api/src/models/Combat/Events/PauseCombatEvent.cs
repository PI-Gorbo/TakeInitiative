namespace TakeInitiative.Api.Models;

public record PauseCombatEvent
{
	public required Guid UserId { get; init; }
};
