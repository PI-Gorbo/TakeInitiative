namespace TakeInitiative.Api.Features.Combats;
public record StagedCharacterEditedEvent : IHistoryVisibleCombatEvent
{
    public required Guid UserId { get; set; }
    public required CombatCharacter Character { get; set; }
}