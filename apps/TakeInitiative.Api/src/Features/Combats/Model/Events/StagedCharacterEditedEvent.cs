namespace TakeInitiative.Api.Models;
public record StagedCharacterEditedEvent
{
	public required Guid UserId {get; set;}
	public required CombatCharacter Character { get; set; }
}