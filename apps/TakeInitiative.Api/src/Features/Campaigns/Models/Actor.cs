namespace TakeInitiative.Api.Features.Campaigns;

/// <summary>
/// Who caused an event (glossary: Actor). It is always a member of the campaign. When the member
/// accepted a suggestion (step 23), <see cref="Model"/> records the model that proposed it: the
/// act is still the member's, and the model is what suggested it (design §9, §11a). Events from
/// before step 23 have no <c>Model</c> in their JSON and read it as null, so nothing migrates.
/// </summary>
public sealed record Actor(Guid MemberId, ModelSuggestion? Model = null)
{
    public static Actor Member(Guid memberId) => new(memberId);

    /// <summary>The member accepting a suggestion from <paramref name="model"/>.</summary>
    public static Actor Suggested(Guid memberId, ModelSuggestion model) => new(memberId, model);
}

/// <summary>
/// The suggestion model behind an accepted suggestion (glossary: Suggestion): its id (e.g.
/// <c>gliner_small-v2.5</c>), its exact version (the pinned weights, e.g.
/// <c>gliner-community/gliner_small-v2.5@&lt;sha&gt;+onnx-int8</c>) and how confident it was,
/// in [0, 1]. Revert works by <see cref="Name"/> and <see cref="Version"/>.
/// </summary>
public sealed record ModelSuggestion(string Name, string Version, double Confidence);

/// <summary>Every event carries the Actor that caused it (invariant 9).</summary>
public interface IActorEvent
{
    Actor Actor { get; }
}
