namespace TakeInitiative.Api.Features.Combats;
public record StagedCharacterEditedEvent
{
    public required Guid UserId { get; set; }
    public required StagedCharacter Character { get; set; }
}