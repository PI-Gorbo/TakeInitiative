using FluentValidation;

namespace TakeInitiative.Api.Features.Combats;
public record CombatCharacter : Character
{
    public required Guid PlayerId { get; set; }
    public required CharacterOriginDetails CharacterOriginDetails { get; set; }
    public required int[] InitiativeValue { get; set; }
    public required bool Hidden { get; set; }
    public required int? CopyNumber { get; set; }
    public Condition[] Conditions { get; set; } = [];

    public static CombatCharacter NewCombatCharacter(Guid playerId, string name, CharacterInitiative initiative, int? armourClass, CharacterHealth? health, bool hidden, CharacterOriginDetails characterOriginDetails, int? copyNumber, Condition[] conditions)
    {
        return new CombatCharacter()
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            CharacterOriginDetails = characterOriginDetails,
            Initiative = initiative,
            Name = name,
            ArmourClass = armourClass,
            Health = health,
            Hidden = hidden,
            InitiativeValue = [],
            CopyNumber = copyNumber,
            Conditions = conditions
        };
    }
}

