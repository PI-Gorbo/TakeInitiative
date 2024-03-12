using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Controllers;

public record PutUpsertStagedPlayerCharacterRequest
{
	public Guid CombatId { get; set; }
	public required StagedCombatCharacterDto Character { get; set; }
}
