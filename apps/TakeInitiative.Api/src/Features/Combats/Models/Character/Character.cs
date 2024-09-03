namespace TakeInitiative.Api.Features.Combats;
public record Character(
    Guid Id,
    string Name,
    UnevaluatedCharacterHealth Health,
    UnevaluatedCharacterInitiative Initiative,
    int? ArmourClass);

public record PlannedCharacter(
    Guid Id,
    string Name,
    UnevaluatedCharacterHealth Health,
    UnevaluatedCharacterInitiative Initiative,
    int? ArmourClass,
    Guid StageId,
    uint Quantity) : Character(Id, Name, Health, Initiative, ArmourClass);

public record PlayerCharacter(
    Guid Id,
    string Name,
    UnevaluatedCharacterHealth Health,
    UnevaluatedCharacterInitiative Initiative,
    int? ArmourClass,
    Guid PlayerId) : Character(Id, Name, Health, Initiative, ArmourClass);

public record StagedCharacter(
    Guid Id,
    string Name,
    UnevaluatedCharacterHealth Health,
    UnevaluatedCharacterInitiative Initiative,
    int? ArmourClass,
    Guid PlayerId,
    CharacterOriginDetails CharacterOriginDetails,
    bool Hidden,
    int? CopyNumber) : Character(Id, Name, Health, Initiative, ArmourClass);

public record InitiativeCharacter(
    Guid Id,
    string Name,
    CharacterHealth Health,
    CharacterInitiative Initiative,
    int? ArmourClass,
    Guid PlayerId,
    CharacterOriginDetails CharacterOriginDetails,
    bool Hidden,
    int? CopyNumber,
    Condition[] Conditions)
{
    public static InitiativeCharacter FromStagedCharacter(StagedCharacter stagedCharacter, CharacterHealth health, CharacterInitiative initiative) => new InitiativeCharacter(
        Id: stagedCharacter.Id,
        Name: stagedCharacter.Name,
        Health: health,
        Initiative: initiative,
        ArmourClass: stagedCharacter.ArmourClass,
        PlayerId: stagedCharacter.PlayerId,
        CharacterOriginDetails: stagedCharacter.CharacterOriginDetails,
        Hidden: stagedCharacter.Hidden,
        CopyNumber: stagedCharacter.CopyNumber,
        Conditions: []
    );
}

