using System.Text.Json.Serialization;

namespace TakeInitiative.Api.Features.Combats;

[JsonDerivedType(typeof(CombatStarted), typeDiscriminator: "CombatStarted")]
[JsonDerivedType(typeof(CombatInitiativeRolled), typeDiscriminator: "CombatInitiativeRolled")]
[JsonDerivedType(typeof(CharacterHealthChanged), typeDiscriminator: "CharacterHealthChanged")]
[JsonDerivedType(typeof(CombatFinished), typeDiscriminator: "CombatFinished")]
[JsonDerivedType(typeof(TurnStarted), typeDiscriminator: "TurnStarted")]
[JsonDerivedType(typeof(TurnEnded), typeDiscriminator: "TurnEnded")]
[JsonDerivedType(typeof(RoundEnded), typeDiscriminator: "RoundEnded")]
[JsonDerivedType(typeof(CharacterRemoved), typeDiscriminator: "CharacterRemoved")]
[JsonDerivedType(typeof(CharactersAddedToInitiative), typeDiscriminator: "CharactersAddedToInitiative")]
[JsonDerivedType(typeof(CharacterConditionAdded), typeDiscriminator: "CharacterConditionAdded")]
[JsonDerivedType(typeof(CharacterConditionRemoved), typeDiscriminator: "CharacterConditionRemoved")]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "!")]
public abstract record HistoryEvent
{
}
