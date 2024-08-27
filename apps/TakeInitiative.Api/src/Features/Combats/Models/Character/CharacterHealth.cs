namespace TakeInitiative.Api.Features;

public record CharacterHealth
{
    public required bool HasHealth { get; set; }
    public required int? MaxHealth { get; set; } = null;
    public required int? CurrentHealth { get; set; } = null;
}