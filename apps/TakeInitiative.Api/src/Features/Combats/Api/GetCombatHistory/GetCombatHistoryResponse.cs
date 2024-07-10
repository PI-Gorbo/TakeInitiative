namespace TakeInitiative.Api.Features.Combats;

public record GetCombatHistoryResponse
{
    public required HistoryEvent[] Events { get; set; }
    public required Guid[] PlayerList { get; set; }
}
