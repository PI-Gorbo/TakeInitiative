using FluentValidation;

namespace TakeInitiative.Api.Features.Combats;

public record CombatCharacterDto
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public required CharacterHealth? Health { get; set; }
    public required bool Hidden { get; set; }
    public required int[] InitiativeValue { get; set; }
    public required int? ArmourClass { get; set; }
    public required Condition[] Conditions { get; set; }
}

public class CombatCharacterDtoValidator : AbstractValidator<CombatCharacterDto>
{
    public CombatCharacterDtoValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.InitiativeValue).NotNull();
        RuleFor(x => x.Conditions).NotEmpty();
    }
}
