using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using static TakeInitiative.Api.Features.Combats.HistoryEvent;

public class PolymorphicTypeResolver : DefaultJsonTypeInfoResolver
{
    public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        JsonTypeInfo jsonTypeInfo = base.GetTypeInfo(type, options);

        Type baseType = typeof(HistoryEvent);
        if (jsonTypeInfo.Type.IsAssignableTo(baseType))
        {
            List<JsonDerivedType> discriminators = [
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
            ];

            jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = "$type",
                IgnoreUnrecognizedTypeDiscriminators = true,
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
            };
            foreach (var discriminator in discriminators.Where(x => x.DerivedType.IsAssignableTo(type)))
            {
                jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(discriminator);
            }
        }

        return jsonTypeInfo;
    }
}