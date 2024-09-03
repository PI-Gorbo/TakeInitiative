namespace TakeInitiative.Api.Features.Combats;

public record StagedPlannedCharacterEvent
{
    public required Guid UserId { get; init; }
    public required Guid CombatId { get; set; }
    public required Dictionary<Guid, StagePlannedCharacterWithIdDto[]> PlannedCharactersToStage { get; set; }
};



