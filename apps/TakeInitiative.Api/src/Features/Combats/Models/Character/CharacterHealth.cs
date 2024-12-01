using System.Text.Json.Serialization;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "!")]
[JsonDerivedType(typeof(Fixed), typeDiscriminator: "Fixed")]
[JsonDerivedType(typeof(None), typeDiscriminator: "None")]
public record CharacterHealth
{
    public record None : CharacterHealth { }
    public record Fixed(int CurrentHealth, int MaxHealth, DiceRoll? DiceRoll) : CharacterHealth { }
}