namespace TakeInitiative.Api.Features.Combats;

public class PutStagePlannedCharactersRequest
{
    public Guid CombatId { get; set; }

    public Dictionary<Guid, StagePlannedCharacterDto[]> PlannedCharactersToStage { get; set; } = null!;
}