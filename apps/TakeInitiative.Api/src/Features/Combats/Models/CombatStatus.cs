using System.Text.Json.Serialization;

namespace TakeInitiative.Api.Features.Combats;

/// <summary>
/// Where a combat is (design §8). A <c>Draft</c> is prep and DM-only; the first roll makes it
/// <c>Active</c> (18b); a DM finishes it, and a <c>Finished</c> combat is read-only.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CombatStatus>))]
public enum CombatStatus
{
    Draft,
    Active,
    Finished,
}
