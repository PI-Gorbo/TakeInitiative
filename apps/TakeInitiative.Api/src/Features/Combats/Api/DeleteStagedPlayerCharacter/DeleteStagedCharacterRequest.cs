namespace TakeInitiative.Api.Features.Combats;

public record DeleteStagedCharacterRequest
{
    public required Guid CharacterId { get; set; }
    public required Guid CombatId { get; set; }
}
