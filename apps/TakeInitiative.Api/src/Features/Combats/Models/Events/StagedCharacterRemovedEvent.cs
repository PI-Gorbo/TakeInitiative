namespace TakeInitiative.Api.Features.Combats;
public record StagedCharacterRemovedEvent : IHistoryVisibleCombatEvent
{
    public required Guid UserId { get; set; }
    public required Guid CharacterId { get; set; }
}

