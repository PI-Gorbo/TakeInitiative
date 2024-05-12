using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Controllers;

public class PutStagePlannedCharactersRequest {
    public Guid CombatId {get; set;}
    
    public Dictionary<Guid, StagePlannedCharacterDto[]> PlannedCharactersToStage {get; set;} = null!;
}
