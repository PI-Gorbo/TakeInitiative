namespace TakeInitiative.Api.Models;

public record PlayerLeftEvent
{
	public required Guid UserId { get; init; }
};
