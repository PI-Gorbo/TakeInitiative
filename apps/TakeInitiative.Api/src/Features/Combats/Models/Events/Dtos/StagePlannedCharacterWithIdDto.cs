namespace TakeInitiative.Api.Features.Combats;

public record StagePlannedCharacterWithIdDto : StagePlannedCharacterDto
{
    public required Guid[] NewGuidsToUse { get; set; }
}
