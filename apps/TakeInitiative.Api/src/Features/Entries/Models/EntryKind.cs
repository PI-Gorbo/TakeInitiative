using System.Text.Json.Serialization;

namespace TakeInitiative.Api.Features.Entries;

/// <summary>An entry's category (glossary: Kind). A closed set, stored and sent as a string.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EntryKind>))]
public enum EntryKind
{
    Character,
    Place,
    Faction,
    Item,
    Event,
    Other,
}
