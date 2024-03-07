namespace TakeInitiative.Api.Models;

public record CombatFinishedEvent
{
	public required Guid UserId { get; init; }
};
