namespace TakeInitiative.Api.Features.Combats;
public record CombatCharacter : Character
{
    public required Guid PlayerId { get; init; }
    public required CharacterOriginDetails CharacterOriginDetails { get; init; }
    public required int[] InitiativeValue { get; init; }
    public required bool Hidden { get; init; }
    public required int? CopyNumber { get; set; }
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

