namespace TakeInitiative.Api.Features.Combats;

public record TurnStarted : HistoryEvent
{
    public required Guid CharacterId { get; set; }
}
