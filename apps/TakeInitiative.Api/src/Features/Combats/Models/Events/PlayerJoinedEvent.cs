namespace TakeInitiative.Api.Features.Combats;

public record PlayerJoinedEvent : IHistoryVisibleCombatEvent
{
    public required Guid UserId { get; init; }
};




