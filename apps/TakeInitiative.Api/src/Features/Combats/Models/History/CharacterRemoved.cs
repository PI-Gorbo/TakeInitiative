namespace TakeInitiative.Api.Features.Combats;

public record CharacterRemoved : HistoryEvent
{
    public required Guid CharacterId { get; set; }
}
