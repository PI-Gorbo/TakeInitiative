namespace TakeInitiative.Api.Features;

public record DeleteInitiativeCharacterRequest
{
    public required Guid CombatId { get; set; }
    public required Guid CharacterId { get; set; }
}
