using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Controllers;

public record PutSetOpenCombatPlayerCharactersRequest
{
	public Guid CombatId { get; set; }
	public required List<CombatPlayerCharacter> Characters { get; set; }
}
