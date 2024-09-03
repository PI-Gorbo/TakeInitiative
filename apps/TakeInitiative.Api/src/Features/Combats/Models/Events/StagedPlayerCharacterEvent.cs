namespace TakeInitiative.Api.Features.Combats;

public record StagedPlayerCharacterEvent
{

    public required Guid UserId { get; set; }
    public required Character[] Characters { get; set; }
    public required Guid[] NewIdsToUse { get; set; }

}