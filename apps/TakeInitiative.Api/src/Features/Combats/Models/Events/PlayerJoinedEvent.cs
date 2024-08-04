namespace TakeInitiative.Api.Features.Combats;

public record PlayerJoinedEvent
{
    public required Guid UserId { get; init; }

};




