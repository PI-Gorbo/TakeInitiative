namespace TakeInitiative.Api.Features.Reference;

/// <summary>
/// A source of reference items (glossary: Reference provider). SRD 5.2 is bundled with the app
/// (step 20, <see cref="SrdReferenceProvider"/>); step 21 adds the 5eTools index as a search-only
/// provider, one more registration in <c>AddReference()</c>. Reference content is the same for
/// every campaign and every member, so nothing here takes a viewer.
/// <para>
/// The three lookups are asynchronous because a provider may read a database (step 26d); a
/// provider whose data is already in memory answers from a completed task.
/// </para>
/// </summary>
public interface IReferenceProvider
{
    /// <summary>The provider's key in routes and in an entry's <c>Source</c>: "srd52", and in step 21 "5etools".</summary>
    string Key { get; }

    /// <summary>What the UI calls it: "SRD 5.2".</summary>
    string Label { get; }

    /// <summary>True when <see cref="Get"/> answers a stat block the app draws itself; false for a search-only provider whose rows link out.</summary>
    bool HasStatBlocks { get; }

    /// <summary>Whom to credit. The SRD's licence statement; for 5eTools a plain note that the row links out, which the web does not show as a licence.</summary>
    ReferenceAttribution Attribution { get; }

    /// <summary>The items whose names match <paramref name="text"/>, best first, at most <paramref name="take"/>.</summary>
    Task<IReadOnlyList<ReferenceMatch>> Search(string text, int take, CancellationToken ct);

    /// <summary>The full item, or null for an unknown id or a search-only provider.</summary>
    Task<ReferenceItem?> Get(string id, CancellationToken ct);

    /// <summary>The item's summary, or null for an unknown id. Unlike <see cref="Get"/>, a search-only provider answers it, which is what + Wiki needs.</summary>
    Task<ReferenceSummary?> Find(string id, CancellationToken ct);
}
