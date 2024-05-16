namespace TakeInitiative.Api.Features.Combats;
public record CombatCharacter : Character
{
    public required Guid PlayerId { get; set; }
    public required CharacterOriginDetails CharacterOriginDetails { get; set; }
    public required int[] InitiativeValue { get; set; }
    public required bool Hidden { get; set; }
    public required int? CopyNumber { get; set; }

    public static CombatCharacter New(string Name, CharacterInitiative initiative, int? ArmorClass = null, CharacterHealth? Health = null, bool hidden = false)
    {
        var character = New(Name, initiative, ArmorClass, Health);
        character.Hidden = hidden;
        return character;
    }

    public static CombatCharacter FromCharacter(Character playerCharacter, Guid playerId, CharacterOriginDetails origin, bool hidden) => new CombatCharacter()
    {
        Id = Guid.NewGuid(),
        PlayerId = playerId,
        CharacterOriginDetails = origin,
        Name = playerCharacter.Name,
        ArmorClass = playerCharacter.ArmorClass,
        CopyNumber = null,
        Health = playerCharacter.Health,
        Initiative = playerCharacter.Initiative,
        InitiativeValue = [],
        Hidden = hidden,
    };
}

