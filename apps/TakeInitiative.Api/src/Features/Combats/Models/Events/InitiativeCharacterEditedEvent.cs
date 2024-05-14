using TakeInitiative.Api.Features;

namespace TakeInitiative.Api.Features.Combats;
public record InitiativeCharacterEditedEvent
{
    public required Guid UserId { get; set; }
    public required CombatCharacterDto Character { get; set; }
}