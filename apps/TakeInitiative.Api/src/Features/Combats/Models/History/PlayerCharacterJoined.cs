namespace TakeInitiative.Api.Features.Combats;

public record PlayerCharacterJoined : HistoryEvent
{
    public required Guid CharacterId { get; set; }
}
