namespace TakeInitiative.Api.Features.Combats;

public record CombatResumedEvent : ICombatEvent
{
    public required Guid UserId { get; init; }
};
