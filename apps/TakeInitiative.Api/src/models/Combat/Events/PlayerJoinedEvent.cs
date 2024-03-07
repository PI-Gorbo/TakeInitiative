namespace TakeInitiative.Api.Models;

public record PlayerJoinedEvent
{
	public required Guid UserId { get; init; }
};




