namespace TakeInitiative.Api.Features.Combats;

public record PostAddStagedCharacterRequest
{
    public Guid CombatId { get; set; }
    public required StagedCombatCharacterWithoutIdDto Character { get; set; }
}
