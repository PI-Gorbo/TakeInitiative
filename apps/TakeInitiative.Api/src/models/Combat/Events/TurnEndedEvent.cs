namespace TakeInitiative.Api.Models;
public record TurnEndedEvent
{
    public required Guid UserId { get; set; }
}

