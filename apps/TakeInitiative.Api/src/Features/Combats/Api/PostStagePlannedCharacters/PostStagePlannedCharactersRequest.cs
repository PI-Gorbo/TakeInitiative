namespace TakeInitiative.Api.Features.Combats;

public class PostStagePlannedCharactersRequest
{
    public Guid CombatId { get; set; }

    public Dictionary<Guid, StagePlannedCharacterDto[]> PlannedCharactersToStage { get; set; } = null!;
}