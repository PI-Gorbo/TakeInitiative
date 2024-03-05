namespace TakeInitiative.Api.Models;

public record PlayerJoinedEvent
{
	public required Guid UserId { get; init; }
	public required List<PlayerCharacter> Characters { get; init; }
};




