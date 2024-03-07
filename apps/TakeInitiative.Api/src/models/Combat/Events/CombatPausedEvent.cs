namespace TakeInitiative.Api.Models;

public record CombatPausedEvent
{
	public required Guid UserId { get; init; }
};
