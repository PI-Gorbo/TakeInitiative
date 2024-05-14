namespace TakeInitiative.Api.Features.Combats;

public class CharacterOriginDetails
{
    public required CharacterOriginOptions CharacterOrigin { get; init; }
    public required Guid? Id { get; set; }
    public static CharacterOriginDetails PlayerCharacter(Guid PlayerCharacterId) => new CharacterOriginDetails() { Id = PlayerCharacterId, CharacterOrigin = CharacterOriginOptions.PlayerCharacter };
    public static CharacterOriginDetails PlannedCharacter(Guid PlannedCharacterId) => new CharacterOriginDetails() { Id = PlannedCharacterId, CharacterOrigin = CharacterOriginOptions.PlayerCharacter };
    public static CharacterOriginDetails CustomCharacter() => new CharacterOriginDetails() { Id = null, CharacterOrigin = CharacterOriginOptions.PlayerCharacter };
}

