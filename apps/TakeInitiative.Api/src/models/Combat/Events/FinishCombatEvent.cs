namespace TakeInitiative.Api.Models;

public record FinishCombatEvent
{
	public required Guid UserId { get; init; }
};
