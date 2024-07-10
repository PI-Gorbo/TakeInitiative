namespace TakeInitiative.Api.Features.Combats;
public record InitiativeCharacterEditedEvent : ICombatEvent
{
    public required Guid UserId { get; set; }
    public required CombatCharacterDto Character { get; set; }
}