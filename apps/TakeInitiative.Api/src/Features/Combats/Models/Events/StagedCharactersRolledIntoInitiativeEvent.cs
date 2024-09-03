namespace TakeInitiative.Api.Features.Combats;
public record StagedCharactersRolledIntoInitiativeEvent
{
    public required Guid UserId { get; set; }
    public required Dictionary<Guid, EvaluatedCharacterRolls> Rolls { get; set; }
}

