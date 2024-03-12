using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Controllers;

public record PutUpsertStagedCharacterRequest
{
	public Guid CombatId { get; set; }
	public required StagedCombatCharacterDto Character { get; set; }
}
