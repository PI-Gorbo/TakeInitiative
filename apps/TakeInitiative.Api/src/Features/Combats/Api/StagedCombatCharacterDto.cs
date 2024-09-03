namespace TakeInitiative.Api.Features.Combats;

public record StagedCombatCharacterDto(
    Guid Id,
    string Name,
    UnevaluatedCharacterHealth Health,
    UnevaluatedCharacterInitiative Initiative,
    int? ArmourClass,
    bool Hidden
) : Character(Id, Name, Health, Initiative, ArmourClass);