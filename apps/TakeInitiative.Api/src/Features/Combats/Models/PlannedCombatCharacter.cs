namespace TakeInitiative.Api.Features.Combats;

public record PlannedCombatCharacter : Character
{
    public required Guid StageId { get; set; }
    public required uint Quantity { get; set; }

    public static PlannedCombatCharacter New(Guid StageId, string Name, CharacterInitiative Initiative, int? ArmourClass, CharacterHealth? Health, uint Quantity)
    {
        return new PlannedCombatCharacter()
        {
            StageId = StageId,
            Id = Guid.NewGuid(),
            Initiative = Initiative,
            Name = Name,
            ArmourClass = ArmourClass,
            Health = Health,
            Quantity = Quantity
        };
    }
}
