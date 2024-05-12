using TakeInitiative.Api.Controllers;

namespace TakeInitiative.Api.Models;
public record InitiativeCharacterRemovedEvent
{
    public required Guid UserId { get; set; }
    public required Guid CharacterId { get; set; }
}