namespace TakeInitiative.Api.Features.Combats;

public record CombatInitiativeModified : HistoryEvent
{
    public required InitiativeRolledDto[] NewInitiativeList { get; set; }
}
