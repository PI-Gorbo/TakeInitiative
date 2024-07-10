namespace TakeInitiative.Api.Features.Combats;

public record CombatFinishedEvent : ICombatEvent
{
    public required Guid UserId { get; init; }
};
