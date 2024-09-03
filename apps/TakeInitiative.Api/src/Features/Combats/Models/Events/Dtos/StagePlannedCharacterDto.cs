namespace TakeInitiative.Api.Features.Combats;

public record StagePlannedCharacterDto
{
    public required Guid CharacterId { get; set; }
    public required uint Quantity { get; set; }
}
