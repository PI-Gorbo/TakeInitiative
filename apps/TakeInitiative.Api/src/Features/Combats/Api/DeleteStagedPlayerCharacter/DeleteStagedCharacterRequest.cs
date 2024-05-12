namespace TakeInitiative.Api.Features;

public record DeleteStagedCharacterRequest
{
    public required Guid CharacterId { get; set; }
    public required Guid CombatId { get; set; }
}
