namespace TakeInitiative.Api.Models;

public record CombatStartedEvent
{
	public required Guid UserId { get; init; }
};
