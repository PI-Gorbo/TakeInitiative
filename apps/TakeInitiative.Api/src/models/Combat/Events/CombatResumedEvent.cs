namespace TakeInitiative.Api.Models;

public record CombatResumedEvent
{
	public required Guid UserId { get; init; }
};
