namespace TakeInitiative.Api.Features.Campaigns;

public record PlayerCharacterDTO
{
    public required string Name { get; set; }
    public required CharacterHealth? Health { get; set; }
    public required CharacterInitiative Initiative { get; set; }
    public required int? ArmorClass { get; set; }
}
