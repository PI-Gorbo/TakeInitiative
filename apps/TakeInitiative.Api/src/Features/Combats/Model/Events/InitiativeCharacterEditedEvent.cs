using TakeInitiative.Api.Features;

namespace TakeInitiative.Api.Models;
public record InitiativeCharacterEditedEvent
{
    public required Guid UserId { get; set; }
    public required CombatCharacterDto Character { get; set; }
}