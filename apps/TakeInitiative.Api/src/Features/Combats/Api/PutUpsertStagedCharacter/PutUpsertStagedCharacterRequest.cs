namespace TakeInitiative.Api.Features.Combats;

public record PutUpsertStagedCharacterRequest
{
    public Guid CombatId { get; set; }
    public required StagedCombatCharacterDto Character { get; set; }
}
