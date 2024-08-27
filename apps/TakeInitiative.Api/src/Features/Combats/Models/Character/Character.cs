namespace TakeInitiative.Api.Features.Combats;
public record Character(
    Guid Id,
    string Name,
    CharacterHealth? Health,
    CharacterInitiative Initiative,
    int? ArmourClass);

public record PlannedCharacter(
    Guid Id,
    string Name,
    CharacterHealth? Health,
    CharacterInitiative Initiative,
    int? ArmourClass,
    Guid StageId,
    uint Quantity) : Character(Id, Name, Health, Initiative, ArmourClass);

public record PlayerCharacter(
    Guid Id,
    string Name,
    CharacterHealth? Health,
    CharacterInitiative Initiative,
    int? ArmourClass,
    Guid PlayerId) : Character(Id, Name, Health, Initiative, ArmourClass);

public record StagedCharacter(
    Guid Id,
    string Name,
    CharacterHealth? Health,
    CharacterInitiative Initiative,
    int? ArmourClass,
    Guid PlayerId,
    CharacterOriginDetails CharacterOriginDetails,
    bool Hidden,
    int? CopyNumber) : Character(Id, Name, Health, Initiative, ArmourClass);

public record InitiativeCharacter(
    Guid Id,
    string Name,
    CharacterHealth? Health,
    CharacterInitiative Initiative,
    int? ArmourClass,
    Guid PlayerId,
    CharacterOriginDetails CharacterOriginDetails,
    bool Hidden,
    int? CopyNumber,
    int[] InitiativeValue,
    Condition[] Conditions) : Character(Id, Name, Health, Initiative, ArmourClass)
{
    public static InitiativeCharacter FromStagedCharacter(StagedCharacter stagedCharacter, int[] InitiativeValue) => new InitiativeCharacter(
        Id: stagedCharacter.Id,
        Name: stagedCharacter.Name,
        Health: stagedCharacter.Health,
        Initiative: stagedCharacter.Initiative,
        ArmourClass: stagedCharacter.ArmourClass,
        PlayerId: stagedCharacter.PlayerId,
        CharacterOriginDetails: stagedCharacter.CharacterOriginDetails,
        Hidden: stagedCharacter.Hidden,
        CopyNumber: stagedCharacter.CopyNumber,
        InitiativeValue: InitiativeValue,
        Conditions: []
    );
}

