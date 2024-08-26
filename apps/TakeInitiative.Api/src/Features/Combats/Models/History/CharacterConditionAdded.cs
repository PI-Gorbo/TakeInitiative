namespace TakeInitiative.Api.Features.Combats;

public record CharacterConditionAdded : HistoryEvent
{
    public required Guid ConditionId { get; set; }
    public required Guid CharacterId { get; set; }
    public required string Name { get; set; }
    public required int RoundNumber { get; set; }
    public required int InitiativeCount { get; set; }
}
