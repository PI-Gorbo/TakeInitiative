namespace TakeInitiative.Api.Features.Combats;

public record CombatInitiativeRolled : HistoryEvent
{
    public required InitiativeRolledDto[] Rolls { get; set; }
}

public record InitiativeRolledDto
{
    public required Guid CharacterId { get; set; }
    public required string CharacterName { get; set; }
    public required int[] Roll { get; set; }
}
