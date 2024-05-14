namespace TakeInitiative.Api.Features.Combats;

public record CombatResponse
{
    public required Combat Combat { get; set; }
}
