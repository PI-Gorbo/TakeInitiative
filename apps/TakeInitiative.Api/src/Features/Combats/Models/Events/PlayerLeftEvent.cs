namespace TakeInitiative.Api.Features.Combats;

public record PlayerLeftEvent
{
    public required Guid UserId { get; init; }
};
