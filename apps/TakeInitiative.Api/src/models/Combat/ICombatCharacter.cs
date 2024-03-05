namespace TakeInitiative.Api.Models;

public interface ICombatCharacter : ICharacter
{
	public int InitiativeValue { get; init; }
	public bool Hidden { get; init; }
}
