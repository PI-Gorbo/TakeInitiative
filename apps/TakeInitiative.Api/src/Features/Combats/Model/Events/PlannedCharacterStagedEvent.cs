using TakeInitiative.Api.CQRS;

namespace TakeInitiative.Api.Models;

public record StagedPlannedCharacterEvent
{
	public required Guid UserId { get; init; }
    public required Guid CombatId {get; set;}
    public required Dictionary<Guid, StagePlannedCharacterDto[]> PlannedCharactersToStage {get; set;}
};



