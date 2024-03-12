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

	// Actual Initiative List
	public ImmutableList<CombatCharacter>? StagedList { get; set; }
	public ImmutableList<CombatCharacter>? InitiativeList { get; set; }

	// Planning 
	public ImmutableList<PlannedCombatStage>? PlannedStages { get; set; }

	public static Combat New(
			Guid Id,
			Guid CampaignId,
			CombatState State,
			string? CombatName,
			Guid DungeonMaster,
			ImmutableList<CombatTimingRecord>? Timing,
			ImmutableList<string>? CombatLogs,
			ImmutableList<PlayerDto>? CurrentPlayers,

			// Actual Initiative List
			ImmutableList<CombatCharacter>? StagedList,
			ImmutableList<CombatCharacter>? InitiativeList,

			// Planning 
			ImmutableList<PlannedCombatStage>? PlannedStages
	)
	{
		return new Combat()
		{
			Id = Id,
			CampaignId = CampaignId,
			State = State,
			CombatName = CombatName,
			DungeonMaster = DungeonMaster,
			Timing = Timing,
			CombatLogs = CombatLogs,
			CurrentPlayers = CurrentPlayers,
			StagedList = StagedList,
			InitiativeList = InitiativeList,
			PlannedStages = PlannedStages
		};
	}
}
