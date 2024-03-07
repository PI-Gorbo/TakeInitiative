using System.Text.Json.Serialization;

namespace TakeInitiative.Api.Models;

[JsonDerivedType(typeof(CombatCharacter), 0)]
[JsonDerivedType(typeof(CombatPlayerCharacter), 1)]
public record CombatCharacter : Character
{
	public int? InitiativeValue { get; init; }
	public bool Hidden { get; init; }
}

public abstract class BaseCombatCharacterValidator<TCombatCharacter> : CharacterValidator<TCombatCharacter>
	where TCombatCharacter : CombatCharacter
{

}

public class CombatCharacterValidator : BaseCombatCharacterValidator<CombatCharacter> { }
