namespace TakeInitiative.Api.Models;

public record StagedCharacterAddedEvent
{
	public required Guid UserId { get; set; }
	public required CombatCharacter Character { get; set; }

}