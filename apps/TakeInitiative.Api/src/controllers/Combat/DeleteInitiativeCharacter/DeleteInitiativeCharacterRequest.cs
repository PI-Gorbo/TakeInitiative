namespace TakeInitiative.Api.Controllers;

public record DeleteInitiativeCharacterRequest
{
    public required Guid CombatId { get; set; }
    public required Guid CharacterId { get; set; }
}
