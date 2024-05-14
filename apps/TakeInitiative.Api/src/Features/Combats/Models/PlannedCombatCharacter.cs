namespace TakeInitiative.Api.Features.Combats;

public record PlannedCombatCharacter : Character
{
    public required Guid StageId { get; set; }
    public required uint Quantity { get; set; }

    public static PlannedCombatCharacter New(Guid StageId, string Name, CharacterInitiative Initiative, int? ArmorClass, CharacterHealth? Health, uint Quantity)
    {
        return new PlannedCombatCharacter()
        {
            StageId = StageId,
            Id = Guid.NewGuid(),
            Initiative = Initiative,
            Name = Name,
            ArmorClass = ArmorClass,
            Health = Health,
            Quantity = Quantity
        };
    }
}
