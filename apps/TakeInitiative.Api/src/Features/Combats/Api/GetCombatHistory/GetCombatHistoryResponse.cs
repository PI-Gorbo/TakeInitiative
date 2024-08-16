namespace TakeInitiative.Api.Features.Combats;

public record GetCombatHistoryResponse
{
    public required HistoryEntry[] History { get; set; }
}
