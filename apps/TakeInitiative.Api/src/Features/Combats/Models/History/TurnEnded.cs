namespace TakeInitiative.Api.Features.Combats;

public record TurnEnded : HistoryEvent
{
    public required Guid CharacterId { get; set; }
}
