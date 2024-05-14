using TakeInitiative.Api.Features;

namespace TakeInitiative.Api.Features.Combats;
public record InitiativeCharacterRemovedEvent
{
    public required Guid UserId { get; set; }
    public required Guid CharacterId { get; set; }
}