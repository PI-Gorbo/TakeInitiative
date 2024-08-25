namespace TakeInitiative.Api.Features.Combats;

public record InitiativeRolledDto
{
    public required Guid CharacterId { get; set; }
    public required string CharacterName { get; set; }
    public required int[] Roll { get; set; }
}
