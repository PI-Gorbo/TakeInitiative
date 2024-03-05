namespace TakeInitiative.Api.Models;

public record PlayerCharactersChangedEvent
{
	public required Guid UserId { get; init; }
	public required List<PlayerCharacter> Characters { get; init; }
};
