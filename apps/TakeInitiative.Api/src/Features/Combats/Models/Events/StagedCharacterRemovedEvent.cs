namespace TakeInitiative.Api.Features.Combats;
public record StagedCharacterRemovedEvent
{
    public required Guid UserId { get; set; }
    public required Guid CharacterId { get; set; }
}

