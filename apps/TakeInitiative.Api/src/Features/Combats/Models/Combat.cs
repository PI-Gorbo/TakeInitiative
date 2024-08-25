using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace TakeInitiative.Api.Features.Combats;

public record Combat
{
    public Guid Id { get; init; }
    public Guid CampaignId { get; set; }
    public CombatState State { get; set; }
    public string? CombatName { get; init; }
    public Guid DungeonMaster { get; init; }
    public ImmutableList<HistoryEntry> History { get; set; } = [];
    public ImmutableList<PlayerDto> CurrentPlayers { get; set; } = [];
    public ImmutableList<PlannedCombatStage> PlannedStages { get; set; } = [];
    public ImmutableList<StagedCharacter> StagedList { get; set; } = [];
    public ImmutableList<InitiativeCharacter> InitiativeList { get; set; } = [];
    public int InitiativeIndex { get; set; }
    public int? RoundNumber { get; set; }
    public DateTimeOffset? FinishedTimestamp { get; set; }

    [JsonConstructor]
    private Combat() { }

    public static Combat New(
            Guid Id,
            Guid CampaignId,
            CombatState State,
            string? CombatName,
            Guid DungeonMaster,
            ImmutableList<HistoryEntry> History,
            ImmutableList<PlayerDto> CurrentPlayers,

            // Actual Initiative List
            ImmutableList<StagedCharacter> StagedList,
            ImmutableList<InitiativeCharacter> InitiativeList,

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
            History = History,
            CurrentPlayers = CurrentPlayers,
            StagedList = StagedList,
            InitiativeList = InitiativeList,
            PlannedStages = PlannedStages,
            InitiativeIndex = -1,
            RoundNumber = null,
            FinishedTimestamp = null
        };
    }

    public (int initiative, int turnNumber) GetNextTurnInfo() => InitiativeIndex + 1 == InitiativeList.Count
        ? (0, (RoundNumber ?? 0) + 1) // At the end of the round, reset to top of initiative and increment round number count.
        : (InitiativeIndex + 1, RoundNumber ?? 0); // Otherwise, just increment initiative index.
}
