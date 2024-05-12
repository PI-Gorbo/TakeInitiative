using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Features;

public record StagedCombatCharacterDto : Character
{
    public bool Hidden { get; init; } = false;
}

