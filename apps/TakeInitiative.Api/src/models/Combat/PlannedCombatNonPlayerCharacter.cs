using FluentValidation;

namespace TakeInitiative.Api.Models;

public record PlannedCombatNonPlayerCharacter : NonPlayerCharacter
{
    public uint Quantity { get; set; } = 1;
}

public class PlannedCombatNonPlayerCharacterValidator : TCharacterValidator {

}
