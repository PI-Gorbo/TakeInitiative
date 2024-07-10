namespace TakeInitiative.Api.Features.Combats;

public record PlayerLeftEvent : IHistoryVisibleCombatEvent
{
    public required Guid UserId { get; init; }
};
