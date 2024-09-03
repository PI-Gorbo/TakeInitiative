namespace TakeInitiative.Api.Features.Combats;

public record PutUpdateStagedCharacterRequest
{
    public Guid CombatId { get; set; }
    public required StagedCombatCharacterDto Character { get; set; }
}
