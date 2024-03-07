namespace TakeInitiative.Api.Models;

public record OpenCombatNpcsAddedEvent {
	public required Guid UserId {get; init;}
	public required List<CombatCharacter> Npcs {get; set;}
}
