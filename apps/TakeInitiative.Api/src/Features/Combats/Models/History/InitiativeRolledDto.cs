using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;

public record InitiativeRolledDto
{
    public required Guid CharacterId { get; set; }
    public required string CharacterName { get; set; }
    public required DiceRoll[] Roll { get; set; }
    public required DiceRoll? RolledHealth { get; set; }
}
