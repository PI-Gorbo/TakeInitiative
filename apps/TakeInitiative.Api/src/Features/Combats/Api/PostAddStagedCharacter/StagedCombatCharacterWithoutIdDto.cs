namespace TakeInitiative.Api.Features.Combats;

public record StagedCombatCharacterWithoutIdDto(
    string Name,
    UnevaluatedCharacterHealth Health,
    UnevaluatedCharacterInitiative Initiative,
    int? ArmourClass,
    bool Hidden
);
