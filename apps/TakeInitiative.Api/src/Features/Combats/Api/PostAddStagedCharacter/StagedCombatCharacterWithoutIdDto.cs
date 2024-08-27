namespace TakeInitiative.Api.Features.Combats;

public record StagedCombatCharacterWithoutIdDto(
    string Name,
    CharacterHealth? Health,
    CharacterInitiative Initiative,
    int? ArmourClass,
    bool Hidden
);
