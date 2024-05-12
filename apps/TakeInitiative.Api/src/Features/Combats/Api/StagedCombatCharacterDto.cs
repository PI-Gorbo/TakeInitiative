using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Controllers;

public record StagedCombatCharacterDto : Character
{
	public bool Hidden { get; init; } = false;
}

