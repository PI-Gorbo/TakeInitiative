namespace TakeInitiative.Api.Features.Combats;

public record StagedCharacterEvent : IHistoryVisibleCombatEvent
{
    public required Guid UserId { get; set; }
    public required CombatCharacter Character { get; set; }

}