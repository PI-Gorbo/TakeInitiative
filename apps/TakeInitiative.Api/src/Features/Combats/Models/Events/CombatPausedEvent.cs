namespace TakeInitiative.Api.Features.Combats;

public record CombatPausedEvent : ICombatEvent
{
    public required Guid UserId { get; init; }
};
