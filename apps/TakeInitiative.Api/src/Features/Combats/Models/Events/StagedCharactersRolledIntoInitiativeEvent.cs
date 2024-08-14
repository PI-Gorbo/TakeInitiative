namespace TakeInitiative.Api.Features.Combats;
public record StagedCharactersRolledIntoInitiativeEvent
{
    public required Guid UserId { get; set; }
    public required List<CharacterInitiativeRoll> InitiativeRolls { get; set; }
}

