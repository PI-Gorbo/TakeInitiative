namespace TakeInitiative.Api.Features.Combats;

public record PostStagePlayerCharactersRequest
{
    public required Guid CombatId { get; set; }
    public required Guid[] CharacterIds { get; set; }

}
