namespace TakeInitiative.Api.Features.Combats;

public record CombatInitiativeRolled : HistoryEvent
{
    public required InitiativeRolledDto[] Rolls { get; set; }
}
