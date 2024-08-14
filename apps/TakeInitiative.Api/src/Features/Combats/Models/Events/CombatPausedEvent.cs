namespace TakeInitiative.Api.Features.Combats;

public record CombatPausedEvent
{
    public required Guid UserId { get; init; }
};
