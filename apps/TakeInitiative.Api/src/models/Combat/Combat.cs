using System.Collections.Immutable;
using JasperFx.CodeGeneration;

namespace TakeInitiative.Api.Models;

public record Combat
{
    public Guid Id { get; init; }
    public Guid CampaignId { get; set; }
    public CombatState State { get; set; }
    public string? CombatName { get; init; }
    public Guid DungeonMaster { get; init; }
    public ImmutableList<CombatTimingRecord> Timing { get; init; } = null!;
    public ImmutableList<string> CombatLogs { get; set; } = null!;
    public ImmutableList<PlayerDto> CurrentPlayers { get; set; } = null!;

    // Actual Initiative List
    public ImmutableList<CombatCharacter> StagedList { get; set; } = null!; // Will never be null when the api has access to it.
    public ImmutableList<CombatCharacter> InitiativeList { get; set; } = null!; // Will never be null when the api has access to it.
    public int InitiativeIndex { get; set; } = -1;
    public int? RoundNumber { get; set; }

    // Planning 
    public ImmutableList<PlannedCombatStage> PlannedStages { get; set; } = null!; // Will never be null when the api has access to it.

    public static Combat New(
            Guid Id,
            Guid CampaignId,
            CombatState State,
            string? CombatName,
            Guid DungeonMaster,
            ImmutableList<CombatTimingRecord> Timing,
            ImmutableList<string> CombatLogs,
            ImmutableList<PlayerDto> CurrentPlayers,

            // Actual Initiative List
            ImmutableList<CombatCharacter> StagedList,
            ImmutableList<CombatCharacter> InitiativeList,

            // Planning 
            ImmutableList<PlannedCombatStage> PlannedStages
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
            PlannedStages = PlannedStages,
            InitiativeIndex = -1,
            RoundNumber = null,
        };
    }

    public (int initiative, int turnNumber) GetNextTurnInfo() =>
    this.InitiativeIndex + 1 == this.InitiativeList.Count
        ? (0, (this.RoundNumber ?? 0) + 1)
        : (this.InitiativeIndex + 1, this.RoundNumber ?? 0);
}
