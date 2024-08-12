using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

public class PolymorphicTypeResolver : DefaultJsonTypeInfoResolver
{
    public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        JsonTypeInfo jsonTypeInfo = base.GetTypeInfo(type, options);

        Type baseType = typeof(ICombatOperation);
        if (type == baseType || jsonTypeInfo.Type.IsAssignableTo(baseType))
        {
            jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = "$type",
                IgnoreUnrecognizedTypeDiscriminators = true,
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
                DerivedTypes =
                {
                    new JsonDerivedType(typeof(CombatOpened), typeDiscriminator: "CombatOpened"),
                    new JsonDerivedType(typeof(CombatStarted), typeDiscriminator: "CombatStarted"),
                    new JsonDerivedType(typeof(CombatFinished), typeDiscriminator: "CombatFinished"),
                    new JsonDerivedType(typeof(TurnStarted), typeDiscriminator: "TurnStarted"),
                    new JsonDerivedType(typeof(TurnEnded), typeDiscriminator: "TurnEnded"),
                    new JsonDerivedType(typeof(RoundEnded), typeDiscriminator: "RoundEnded"),
                    new JsonDerivedType(typeof(PlayerCharacterJoined), typeDiscriminator: "PlayerCharacterJoined"),
                    new JsonDerivedType(typeof(PlannedCharactersAdded), typeDiscriminator: "PlannedCharactersAdded"),
                    new JsonDerivedType(typeof(CharacterRemoved), typeDiscriminator: "CharacterRemoved"),
                    new JsonDerivedType(typeof(CharacterHealthChanged), typeDiscriminator: "CharacterHealthChanged"),
                }
            };
        }

        return jsonTypeInfo;
    }
}