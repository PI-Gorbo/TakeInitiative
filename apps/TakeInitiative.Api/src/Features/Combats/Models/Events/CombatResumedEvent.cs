namespace TakeInitiative.Api.Features.Combats;

public record CombatResumedEvent
{
    public required Guid UserId { get; init; }
};
