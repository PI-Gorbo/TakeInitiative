namespace TakeInitiative.Api.Features.Combats;

public record CharacterConditionRemoved : HistoryEvent
{
    public required Guid ConditionId { get; set; }
    public required Guid CharacterId { get; set; }
    public required string Name { get; set; }
}
