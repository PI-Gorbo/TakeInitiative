using System.Text.Json.Serialization;

namespace TakeInitiative.Api.Features.Combats;

/// <summary>
/// Per combatant, what players see of its HP (glossary: PlayersSee). AC shows only with
/// <c>Exact</c>. The owner of a combatant always sees it exactly.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlayersSee>))]
public enum PlayersSee
{
    /// <summary>HP, max HP and AC.</summary>
    Exact,
    /// <summary>Only the <see cref="HpBand"/>.</summary>
    Band,
    /// <summary>No HP, no band and no AC.</summary>
    Nothing,
}
