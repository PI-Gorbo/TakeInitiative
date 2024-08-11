namespace TakeInitiative.Api.Features.Combats;
public record TurnEndedEvent : ICombatEvent
{
    public required Guid UserId { get; set; }
}

