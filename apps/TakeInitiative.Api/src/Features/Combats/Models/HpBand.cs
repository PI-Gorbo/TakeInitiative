using System.Text.Json.Serialization;

namespace TakeInitiative.Api.Features.Combats;

/// <summary>
/// A coarse HP reading (glossary: HP band), shown to players for a <see cref="PlayersSee.Band"/>
/// combatant: <c>Down</c> at 0 or less, <c>Bloodied</c> at half or less (integer division,
/// so 7 of 15 is Bloodied), <c>Healthy</c> above. No band without a max HP.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<HpBand>))]
public enum HpBand
{
    Healthy,
    Bloodied,
    Down,
}

public static class HpBands
{
    public static HpBand? Of(int? hp, int? maxHp) => (hp, maxHp) switch
    {
        (_, null) => null,
        (null, _) => null,
        ({ } h, _) when h <= 0 => HpBand.Down,
        ({ } h, { } max) when h <= max / 2 => HpBand.Bloodied,
        _ => HpBand.Healthy,
    };
}
