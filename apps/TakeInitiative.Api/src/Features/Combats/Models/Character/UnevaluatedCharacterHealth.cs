using System.Text.Json.Serialization;

namespace TakeInitiative.Api.Features;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "!")]
[JsonDerivedType(typeof(Roll), typeDiscriminator: "Roll")]
[JsonDerivedType(typeof(Fixed), typeDiscriminator: "Fixed")]
[JsonDerivedType(typeof(None), typeDiscriminator: "None")]
public abstract record UnevaluatedCharacterHealth
{
    public record None() : UnevaluatedCharacterHealth { };

    public record Roll(string? RollString) : UnevaluatedCharacterHealth { };

    public record Fixed(int CurrentHealth, int MaxHealth) : UnevaluatedCharacterHealth { };
}
