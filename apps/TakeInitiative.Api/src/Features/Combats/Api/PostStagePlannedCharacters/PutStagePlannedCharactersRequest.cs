using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Features;

public class PutStagePlannedCharactersRequest
{
    public Guid CombatId { get; set; }

    public Dictionary<Guid, StagePlannedCharacterDto[]> PlannedCharactersToStage { get; set; } = null!;
}
