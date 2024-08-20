namespace TakeInitiative.Api.Features.Combats;

public record CombatInitiativeRolled : HistoryEvent
{
    public required CharacterInitiativeRoll[] Rolls { get; set; }
    public required Dictionary<Guid, string> CharacterNames { get; set; }
}

public record InitiativeRolledDto
{
    public required Guid CharacterId { get; set; }
    public required string CharacterNames { get; set; }
    public required int[] Roll { get; set; }
}
