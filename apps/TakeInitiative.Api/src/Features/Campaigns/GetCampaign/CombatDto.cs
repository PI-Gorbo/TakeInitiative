using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Controllers;

public record CombatDto
{
	public required Guid Id { get; set; }
	public required CombatState State { get; set; }
	public required string? CombatName { get; set; }
	public required Guid DungeonMaster { get; set; }
	public required List<PlayerDto> CurrentPlayers { get; set; }
}
