namespace TakeInitiative.Api.Features.Combats;

public record CombatInitiativeRolled : HistoryEvent
{
    public required CharacterInitiativeRoll[] Rolls { get; set; }
}
