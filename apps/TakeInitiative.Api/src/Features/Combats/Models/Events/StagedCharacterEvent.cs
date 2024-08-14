namespace TakeInitiative.Api.Features.Combats;

public record StagedCharacterEvent
{
    public required Guid UserId { get; set; }
    public required CombatCharacter Character { get; set; }

}