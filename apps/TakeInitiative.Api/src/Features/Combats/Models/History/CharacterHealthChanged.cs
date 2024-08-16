namespace TakeInitiative.Api.Features.Combats;

public record CharacterHealthChanged : HistoryEvent
{
    public required Guid CharacterId { get; set; }
    public required int From { get; set; }
    public required int To { get; set; }
}
