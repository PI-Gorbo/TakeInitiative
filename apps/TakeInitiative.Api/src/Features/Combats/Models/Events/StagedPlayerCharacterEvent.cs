namespace TakeInitiative.Api.Features.Combats;

public record StagedPlayerCharacterEvent : IHistoryVisibleCombatEvent
{

    public required Guid UserId { get; set; }
    public required Character[] Characters { get; set; }

}