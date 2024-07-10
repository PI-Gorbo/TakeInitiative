namespace TakeInitiative.Api.Features.Combats;

public record StagedPlannedCharacterEvent : ICombatEvent
{
    public required Guid UserId { get; init; }
    public required Guid CombatId { get; set; }
    public required Dictionary<Guid, StagePlannedCharacterDto[]> PlannedCharactersToStage { get; set; }
};



