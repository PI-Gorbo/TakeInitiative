using System.Collections.Immutable;

namespace TakeInitiative.Api.Models;

public record Combat
{
	public Guid Id { get; init; }
	public Guid CampaignId { get; set; }
	public CombatState State { get; set; }
	public string? CombatName { get; init; }
	public Guid DungeonMaster { get; init; }
	public ImmutableList<CombatTimingRecord>? Timing { get; init; }
	public ImmutableList<string>? CombatLogs { get; set; }
	public ImmutableList<PlayerDto>? CurrentPlayers { get; set; }
	public ImmutableList<PlannedCombatStage>? PlannedStages { get; set; }
	public ImmutableList<ICombatCharacter>? InitiativeList { get; set; }
}
