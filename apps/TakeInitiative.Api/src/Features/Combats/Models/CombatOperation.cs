using System.Text.Json.Serialization;

namespace TakeInitiative.Api.Features.Combats;

public interface ICombatOperation
{

}

public record CombatOpened() : ICombatOperation { }
public record CombatStarted() : ICombatOperation { }
public record CombatFinished() : ICombatOperation { }
public record TurnStarted(Guid CharacterId) : ICombatOperation { }
public record TurnEnded(Guid CharacterId) : ICombatOperation { }
public record RoundEnded() : ICombatOperation { }
public record PlayerCharacterJoined(Guid CharacterId) : ICombatOperation { }
public record PlannedCharactersAdded() : ICombatOperation { }
public record CharacterRemoved(Guid CharacterId) : ICombatOperation { }
public record CharacterHealthChanged(Guid CharacterId, int From, int To) : ICombatOperation { }
