namespace TakeInitiative.Api.Features.Combats;

public record StagedCombatCharacterDto : Character
{
    public bool Hidden { get; init; } = false;
}

