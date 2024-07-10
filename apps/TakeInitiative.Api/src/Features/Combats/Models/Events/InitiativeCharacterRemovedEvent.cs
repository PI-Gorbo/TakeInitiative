namespace TakeInitiative.Api.Features.Combats;
public record InitiativeCharacterRemovedEvent : ICombatEvent
{
    public required Guid UserId { get; set; }
    public required Guid CharacterId { get; set; }
}