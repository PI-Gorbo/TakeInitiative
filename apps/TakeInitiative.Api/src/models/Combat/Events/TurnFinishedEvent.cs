namespace TakeInitiative.Api.Models;
public record TurnFinishedEvent
{
    public required Guid UserId { get; set; }
}

