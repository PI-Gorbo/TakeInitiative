namespace TakeInitiative.Api.Models;

public record OpenCombatPlayerCharactersSetEvent
{
	public required Guid UserId { get; init; }
	public required List<CombatPlayerCharacter> Characters { get; init; }
};
