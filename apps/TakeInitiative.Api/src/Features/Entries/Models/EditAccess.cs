using System.Text.Json.Serialization;

namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// Who can edit an entry (glossary: Edit access): <c>Anyone</c> who can see it, or
/// <c>OnlyMe</c>, the creator. DMs can always edit (invariant 4). Stored and sent as a string.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<EditAccess>))]
public enum EditAccess
{
    Anyone,
    OnlyMe,
}
