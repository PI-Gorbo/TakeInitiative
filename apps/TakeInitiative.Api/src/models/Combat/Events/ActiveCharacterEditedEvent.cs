namespace TakeInitiative.Api.Models;

public record ActiveCharacterEditedEvent
{
	public required Guid UserId { get; set; }
	public required CombatCharacter Character { get; set; }
}

