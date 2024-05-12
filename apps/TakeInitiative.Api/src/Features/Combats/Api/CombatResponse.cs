using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Features;

public record CombatResponse
{
    public required Combat Combat { get; set; }
}
