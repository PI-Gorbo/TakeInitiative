namespace TakeInitiative.Api.Features.Combats;

public record PutUpdateInitiativeCharacterRequest
{
    public required Guid CombatId { get; set; }
    public required CombatCharacterDto Character { get; set; }
}
