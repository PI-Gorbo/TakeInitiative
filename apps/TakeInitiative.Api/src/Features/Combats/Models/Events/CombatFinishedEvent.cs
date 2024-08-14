namespace TakeInitiative.Api.Features.Combats;

public record CombatFinishedEvent
{
    public required Guid UserId { get; init; }
};
