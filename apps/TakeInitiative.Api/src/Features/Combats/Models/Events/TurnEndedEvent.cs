namespace TakeInitiative.Api.Features.Combats;
public record TurnEndedEvent
{
    public required Guid UserId { get; set; }
}

