namespace TakeInitiative.Api.Models;

public record ActiveCharacterAddedEvent
{
	public required Guid UserId { get; set; }
	public required CombatCharacter Character { get; set; }
	public int Initiative { get; set; }
}

