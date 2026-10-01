using TakeInitiative.Api.Features.Reference;
using TakeInitiative.Api.Features.Reference.KnowledgeBase;

namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// The knowledge-base rows an entry might be (28b), best first, and <b>empty is the normal
/// answer</b>: most entries look like nothing in the corpus, and a member who may not see
/// suggestions gets the same empty list rather than a 403 (<see cref="EntrySuggestions.CanSee" />).
/// <para>
/// The rows are <see cref="KnowledgeBaseItemResponse" />, the same shape 26e's browse page sends, on
/// purpose: the prompt shows the row's own detail line to let the member judge, the web already draws
/// that shape, and "Link it" has everything <c>POST links</c> needs without a second request. There
/// is no score and no rung on it — a trigram similarity is not a probability, and showing one would
/// imply more than it means.
/// </para>
/// </summary>
public record EntryKnowledgeBaseSuggestionsResponse
{
    /// <summary>At most <see cref="EntrySuggestions.MaxSuggestions" /> rows, best first.</summary>
    public required KnowledgeBaseItemResponse[] Items { get; init; }

    /// <summary>The empty answer, which is also what "not for you" looks like.</summary>
    public static EntryKnowledgeBaseSuggestionsResponse None { get; } = new() { Items = [] };

    /// <summary>
    /// What <paramref name="viewer" /> is offered for <paramref name="entry" />: the suggester's rows
    /// when they may see suggestions at all, and nothing — not a 403 — when they may not. Both
    /// endpoints answer through here so the two cannot drift apart on the one rule that matters.
    /// </summary>
    public static async Task<EntryKnowledgeBaseSuggestionsResponse> For(
        Entry entry,
        Member viewer,
        KnowledgeBaseSuggester suggester,
        ReferenceCatalog catalog,
        CancellationToken ct)
    {
        if (!EntrySuggestions.CanSee(entry, viewer))
        {
            return None;
        }

        var rows = await suggester.For(entry, ct);
        return new EntryKnowledgeBaseSuggestionsResponse
        {
            Items = [.. rows.Select(row => KnowledgeBaseItemResponse.From(row, catalog.Get(row.Provider)?.Label))],
        };
    }
}
