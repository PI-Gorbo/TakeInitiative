using System.Text.Json.Serialization;

namespace TakeInitiative.Api.Features.Campaigns;

/// <summary>
/// Who can see something (glossary: Visibility): <c>Everyone</c>, <c>DM</c> (all DMs
/// plus the author) or <c>Me</c> (the author only). Session notes use it now; entries
/// and secret blocks use it from step 15. Stored and sent as a string.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<Visibility>))]
public enum Visibility
{
    Everyone,
    DM,
    Me,
}
