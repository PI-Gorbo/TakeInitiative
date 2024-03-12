namespace TakeInitiative.Api.Models;

public record CombatActivatedEvent
{
	public required Guid UserId { get; init; }
};
