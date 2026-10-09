using FastEndpoints;
using FluentValidation;

using Marten;

using TakeInitiative.Api.Features.Reference;
using TakeInitiative.Api.Features.Reference.KnowledgeBase;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Entries;

/// <summary>The row the member said this entry is not (28b): both halves of its key.</summary>
public record PostEntrySuggestionDismissRequest
{
    public Guid CampaignId { get; init; }
    public Guid EntryId { get; init; }
    /// <summary>A reference provider's key: <c>5etools</c>.</summary>
    public required string Provider { get; init; }
    /// <summary>The row's id within that provider: <c>monster_beholder_mm</c>.</summary>
    public required string ItemId { get; init; }
}

/// <summary>
/// Both fields are always required, so <c>NotEmpty</c> is the right rule here: FastEndpoints reads it
/// off the validator and marks the property <c>required</c> in the OpenAPI document, which is true of
/// these two. (That mapping is what <c>PostEntryLinkRequestValidator</c> has to avoid, because its
/// fields are only required <i>inside a <c>When</c></i> and the generator does not read the condition.)
/// </summary>
public class PostEntrySuggestionDismissRequestValidator : Validator<PostEntrySuggestionDismissRequest>
{
    public PostEntrySuggestionDismissRequestValidator()
    {
        RuleFor(x => x.Provider).NotEmpty().MaximumLength(EntrySuggestions.ProviderMaxLength);
        RuleFor(x => x.ItemId).NotEmpty().MaximumLength(EntrySuggestions.ItemIdMaxLength);
    }
}

/// <summary>
/// "No, this entry is not that" (28b): appends <see cref="EntryKnowledgeBaseSuggestionDismissed" />,
/// so the prompt never offers that row for this entry again, and answers the suggestions that are
/// left.
/// <list type="bullet">
/// <item><b>Idempotent.</b> Dismissing a row this entry has already dismissed appends nothing and is
/// still a 200: the member's answer has not changed, and the prompt they tapped may simply have been
/// on screen twice.</item>
/// <item><b>Authorised as the write path</b> — <see cref="EntryLinks.CanWrite" />, through
/// <c>RequireCanWriteLinks</c>, and deliberately <i>not</i> that and <see cref="EntryLinks.CanRead" />.
/// Writing a dismissal discloses nothing; adding the read check would only make this endpoint
/// disagree with <c>POST links</c>, which is the endpoint the other button calls.</item>
/// <item><b>Any key is accepted</b> within its length cap: there is no check that the provider is
/// registered or that the row is there. A dismissal is a statement about this entry, it costs one
/// small event, and refusing one for a row that has since been pruned would leave the member unable
/// to silence a prompt they can see.</item>
/// </list>
/// <para>
/// The answer goes through <see cref="EntryKnowledgeBaseSuggestionsResponse.For" />, so a caller who
/// may write but not read gets <c>[]</c> here as well, and the web can treat the response exactly as
/// it treats the <c>GET</c>'s. Nothing is pushed: a dismissal moves no summary and changes no link
/// (invariant 5), and the other DM's page reads it the next time it asks.
/// </para>
/// </summary>
public class PostEntrySuggestionDismiss(
    IDocumentSession session, KnowledgeBaseSuggester suggester, ReferenceCatalog reference)
    : Endpoint<PostEntrySuggestionDismissRequest, EntryKnowledgeBaseSuggestionsResponse>
{
    public override void Configure()
    {
        Post("/api/campaigns/{CampaignId}/entries/{EntryId}/knowledge-base-suggestions/dismiss");
    }

    public override async Task HandleAsync(PostEntrySuggestionDismissRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var entry = await this.RequireVisibleEntry(session, req.CampaignId, req.EntryId, member, ct);
        this.RequireCanWriteLinks(entry, member);

        // The provider's own spelling of its key, as POST links stores it, so a dismissal sent as
        // "5eTools" and a link added as "5etools" are about the same row. An unregistered provider
        // keeps the spelling it was given: there is nothing better to fold it to.
        var provider = (req.Provider ?? "").Trim();
        provider = reference.Get(provider)?.Key ?? provider;
        var itemId = (req.ItemId ?? "").Trim();

        if (!entry.DismissedSuggestions.Any(d => d.Is(provider, itemId)))
        {
            session.Events.Append(entry.Id, new EntryKnowledgeBaseSuggestionDismissed(Actor.Member(member.MemberId), provider, itemId));
            await session.SaveChangesAsync(ct);
            entry = (await session.LoadAsync<Entry>(entry.Id, ct))!;
        }

        await SendAsync(
            await EntryKnowledgeBaseSuggestionsResponse.For(entry, member, suggester, reference, ct),
            cancellation: ct);
    }
}
