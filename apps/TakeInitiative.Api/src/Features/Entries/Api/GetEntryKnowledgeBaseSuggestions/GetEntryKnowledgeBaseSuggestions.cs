using FastEndpoints;

using Marten;

using TakeInitiative.Api.Features.Reference;
using TakeInitiative.Api.Features.Reference.KnowledgeBase;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Entries;

public record GetEntryKnowledgeBaseSuggestionsRequest
{
    public Guid CampaignId { get; init; }
    public Guid EntryId { get; init; }
}

/// <summary>
/// The knowledge-base rows this entry might be (28b): at most
/// <see cref="EntrySuggestions.MaxSuggestions" />, best first, for the members who could act on one.
/// <list type="bullet">
/// <item>Who may see them is <see cref="EntrySuggestions.CanSee" />: the entry's links read rule
/// <b>and</b> write access. The read half stops the disclosure — "Is this the Beholder?" on an
/// unclaimed Character names the monster as completely as its stat block would — and the write half
/// stops a question whose only answer is "dismiss".</item>
/// <item><b>Anyone else gets <c>[]</c>, not a 403.</b> A 403 would say "there is something here you
/// may not see", which is the same leak with a status code in front of it (invariant 5). The entry
/// itself still decides 404: an entry the caller cannot see is not here, as everywhere else.</item>
/// <item>Nothing is stored and nothing is suggested in the background. This endpoint answers a
/// question the entry page asked, and <c>POST …/links</c> is what acts on the answer.</item>
/// </list>
/// <para>
/// <b>No <c>Cache-Control</c>.</b> Unlike 26e's browse page this answer is not a slice of the corpus:
/// it changes when the entry is renamed, given an alias, re-kinded, linked or dismissed, so a cached
/// copy would be a prompt that will not go away when the member deals with it.
/// </para>
/// </summary>
public class GetEntryKnowledgeBaseSuggestions(
    IDocumentSession session, KnowledgeBaseSuggester suggester, ReferenceCatalog reference)
    : Endpoint<GetEntryKnowledgeBaseSuggestionsRequest, EntryKnowledgeBaseSuggestionsResponse>
{
    public override void Configure()
    {
        Get("/api/campaigns/{CampaignId}/entries/{EntryId}/knowledge-base-suggestions");
    }

    public override async Task HandleAsync(GetEntryKnowledgeBaseSuggestionsRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var entry = await this.RequireVisibleEntry(session, req.CampaignId, req.EntryId, member, ct);

        await SendAsync(
            await EntryKnowledgeBaseSuggestionsResponse.For(entry, member, suggester, reference, ct),
            cancellation: ct);
    }
}
