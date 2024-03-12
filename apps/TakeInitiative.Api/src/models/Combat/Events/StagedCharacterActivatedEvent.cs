namespace TakeInitiative.Api.Models;
public record StagedCharacterActivatedEvent
{
	public required Guid UserId { get; set; }
	public required Guid CharacterId { get; set; }
	public required int Initiative { get; set; }
}

