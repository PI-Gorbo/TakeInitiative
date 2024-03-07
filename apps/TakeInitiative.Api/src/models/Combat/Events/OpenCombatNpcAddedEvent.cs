using System.Diagnostics.CodeAnalysis;

namespace TakeInitiative.Api.Models;

public record OpenCombatNpcAddedEvent
{
	public required Guid UserId { get; init; }
	public required CombatCharacter Npc { get; init; }
};
