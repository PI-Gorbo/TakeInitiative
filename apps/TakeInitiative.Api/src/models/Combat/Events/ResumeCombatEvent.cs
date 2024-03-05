namespace TakeInitiative.Api.Models;

public record ResumeCombatEvent
{
	public required Guid UserId { get; init; }
};
