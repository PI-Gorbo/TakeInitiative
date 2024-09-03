namespace TakeInitiative.Api.Features.Campaigns;

public record PlayerCharacterDTO
{
    public required string Name { get; set; }
    public required UnevaluatedCharacterHealth Health { get; set; }
    public required UnevaluatedCharacterInitiative Initiative { get; set; }
    public int? ArmourClass { get; set; }
}
