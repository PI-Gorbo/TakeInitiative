using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// A link to add (27c). <see cref="Kind"/> says which of the other fields matter, and the ones it
/// does not are ignored rather than refused: a client that keeps both halves of its form filled in
/// while the user flips between the two tabs is not making a mistake.
/// </summary>
public record PostEntryLinkRequest
{
    public Guid CampaignId { get; init; }
    public Guid EntryId { get; init; }
    /// <summary>Which kind of link this is.</summary>
    public required EntryLinkKind Kind { get; init; }
    /// <summary>For <c>KnowledgeBase</c>: a registered reference provider's key, <c>5etools</c>.</summary>
    public string? Provider { get; init; }
    /// <summary>For <c>KnowledgeBase</c>: the row's id within that provider, <c>monster_beholder_mm</c>.</summary>
    public string? ItemId { get; init; }
    /// <summary>For <c>External</c>: an absolute <c>http</c> or <c>https</c> url.</summary>
    public string? Url { get; init; }
    /// <summary>For <c>External</c>: what to call it. Required, and never rendered as markdown.</summary>
    public string? Label { get; init; }
}

/// <summary>
/// Everything that can be decided without the database (27c, "Validation"):
/// <list type="bullet">
/// <item><c>External</c> needs a url that <see cref="EntryLinkUrl.Parse"/> accepts — absolute,
/// <c>http</c> or <c>https</c>, at most <see cref="EntryLinks.UrlMaxLength"/> characters. That is
/// what refuses <c>javascript:</c>, <c>data:</c> and <c>file:</c>, and it happens <b>here</b>: a
/// url the validator lets through is a url some view will eventually put in an <c>href</c>, so
/// filtering at render time would be one forgotten component away from an injection.</item>
/// <item><c>External</c> needs a label, trimmed, at most <see cref="EntryLinks.LabelMaxLength"/>
/// characters. A link with no label is a bare url in the UI, and the label is the only thing that
/// says what it is for.</item>
/// <item><c>KnowledgeBase</c> needs both halves of its key. Whether the provider is registered and
/// the row is there is the endpoint's business, because it needs the catalog to find out.</item>
/// </list>
/// </summary>
public class PostEntryLinkRequestValidator : Validator<PostEntryLinkRequest>
{
    public PostEntryLinkRequestValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();

        When(x => x.Kind == EntryLinkKind.KnowledgeBase, () =>
        {
            RuleFor(x => x.Provider).Must(Present).WithMessage("Name a reference provider.");
            RuleFor(x => x.ItemId).Must(Present).WithMessage("Name a reference item.");
        });

        // Two rules per field rather than one chain with a When: a trailing When applies to every
        // validator in its chain, which silently turns "a url is required" off. Each "is it allowed"
        // predicate is total instead — vacuously true for a blank value — so a blank field is one
        // error ("paste a link") and a bad one is the other, and neither depends on rule order.
        When(x => x.Kind == EntryLinkKind.External, () =>
        {
            RuleFor(x => x.Url).Must(Present).WithMessage("Paste a link.");
            RuleFor(x => x.Url)
                .Must(url => string.IsNullOrWhiteSpace(url) || EntryLinkUrl.IsAllowed(url))
                .WithMessage($"A link must be a http:// or https:// address of at most {EntryLinks.UrlMaxLength:N0} characters.");
            RuleFor(x => x.Label).Must(Present).WithMessage("Give the link a label.");
            RuleFor(x => x.Label)
                .Must(label => (label ?? "").Trim().Length <= EntryLinks.LabelMaxLength)
                .WithMessage($"A label can be at most {EntryLinks.LabelMaxLength} characters long.");
        });
    }

    /// <summary>
    /// "There is something here", as <c>NotEmpty</c> would say it — and deliberately <b>not</b>
    /// <c>NotEmpty</c>. FastEndpoints' schema generator reads <c>NotEmpty</c> off the validator and
    /// marks the property <c>required</c> in the OpenAPI document, and it does not read the
    /// surrounding <c>When</c>. All four conditional fields came out required, which made the
    /// generated <c>PostEntryLinkRequest</c> demand a <c>url</c> and a <c>label</c> on a
    /// knowledge-base link and a <c>provider</c> on an external one. A <c>Must</c> is not mapped, so
    /// the document says what is true: only <c>kind</c> is always required, and which of the rest
    /// matters depends on it.
    /// </summary>
    private static bool Present(string? value) => !string.IsNullOrWhiteSpace(value);
}

/// <summary>
/// Adds a link to an entry (glossary: Link, 27c). Two kinds, one list, one shape.
/// <list type="bullet">
/// <item>Who may write is <see cref="EntryLinks.CanWrite"/> (403 otherwise): the article's rule,
/// invariant 4, plus the member who plays a claimed Character, so a DM tightening edit access
/// cannot take a player's own sheet link away (27d). Who may <i>read</i> what was written is
/// <see cref="EntryLinks.CanRead"/>, which is a different and stricter question.</item>
/// <item>At most <see cref="EntryLinks.MaxPerEntry"/> links on an entry. A 21st is a 409
/// <c>errors.links</c>.</item>
/// <item>A <c>KnowledgeBase</c> link's provider must be registered and its row must be there now
/// (404 <c>errors.itemId</c>, as + Wiki answers the same question). A row that disappears
/// <i>later</i> is the stale case, not a validation failure.</item>
/// <item>A duplicate — the same <c>(provider, itemId)</c>, or a url that normalises to one already
/// on the entry — is a 409 rather than a silent no-op, because the member meant to add something
/// and nothing would have happened.</item>
/// </list>
/// The response is the caller's view of the entry, and <c>entryLinksChanged</c> goes only to the
/// members who may read the links, so adding "Beholder" to an unclaimed Character pings the DMs
/// alone. Links leave <c>updatedAt</c> alone, so nothing on the summary moves (invariant 5).
/// </summary>
public class PostEntryLink(IDocumentSession session, IHubContext<CampaignHub> hub, ReferenceCatalog reference, EntryLinkResolver links)
    : Endpoint<PostEntryLinkRequest, EntryResponse>
{
    public const string LinksErrorKey = "links";
    public const string ItemIdErrorKey = "itemId";
    public const string UrlErrorKey = "url";

    public override void Configure()
    {
        Post("/api/campaigns/{CampaignId}/entries/{EntryId}/links");
        // No ProducesProblemFE(403) here, and none anywhere else in the API: FastEndpoints already
        // declares a 403 for an authorised endpoint with no content, and adding a second one emits a
        // `application/problem+json: null` media type that openapi-typescript refuses to read, which
        // breaks `gen:api` rather than the build.
        Description(b => b
            .ProducesProblemFE(StatusCodes.Status404NotFound)
            .ProducesProblemFE(StatusCodes.Status409Conflict));
    }

    public override async Task HandleAsync(PostEntryLinkRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (campaign, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var entry = await this.RequireVisibleEntry(session, req.CampaignId, req.EntryId, member, ct);
        this.RequireCanWriteLinks(entry, member);

        // The cap before the lookups: it is a fact about the entry, so a 21st link is refused without
        // a query for the row it names. The request's own shape was already settled by the validator,
        // so a 21st link that is also malformed is that 400 rather than this 409.
        if (entry.Links.Count >= EntryLinks.MaxPerEntry)
        {
            ThrowError(
                new ValidationFailure(LinksErrorKey, $"An entry can have at most {EntryLinks.MaxPerEntry} links. Remove one first."),
                StatusCodes.Status409Conflict);
        }

        var link = req.Kind == EntryLinkKind.KnowledgeBase
            ? await KnowledgeBaseLink(req, entry, member, ct)
            : ExternalLink(req, entry, member);

        session.Events.Append(entry.Id, new EntryLinkAdded(Actor.Member(member.MemberId), link));
        await session.SaveChangesAsync(ct);
        var before = entry;
        entry = (await session.LoadAsync<Entry>(entry.Id, ct))!;
        await hub.NotifyEntryLinksChanged(campaign.Members, before, entry);

        await SendAsync(await EntryResponse.From(entry, member, reference, links, ct), cancellation: ct);
    }

    /// <summary>
    /// The knowledge-base link, once the provider and the row have been found. Nothing about the row
    /// is kept: only the two halves of its key, which is the point (see <see cref="EntryLink"/>).
    /// </summary>
    private async Task<EntryLink> KnowledgeBaseLink(PostEntryLinkRequest req, Entry entry, Member member, CancellationToken ct)
    {
        // The validator guaranteed both are there; this is what turns them into non-null locals.
        var providerKey = (req.Provider ?? "").Trim();
        var itemId = (req.ItemId ?? "").Trim();

        var provider = reference.Get(providerKey);
        var item = provider is null ? null : await provider.Find(itemId, ct);
        if (provider is null || item is null)
        {
            ThrowError(new ValidationFailure(ItemIdErrorKey, "That isn't in the reference data."), StatusCodes.Status404NotFound);
        }

        // The provider's own spelling of its key, so two links added as "5etools" and "5eTools" are
        // one duplicate rather than two rows that resolve to the same thing.
        var key = provider.Key;
        if (entry.Links.Any(l => l is { Kind: EntryLinkKind.KnowledgeBase, Provider: { } p, ItemId: { } id }
                && string.Equals(p, key, StringComparison.OrdinalIgnoreCase)
                && string.Equals(id, item.Id, StringComparison.Ordinal)))
        {
            ThrowError(
                new ValidationFailure(ItemIdErrorKey, $"\"{item.Name}\" is already linked from this entry."),
                StatusCodes.Status409Conflict);
        }

        return New(EntryLinkKind.KnowledgeBase, key, item.Id, null, null, member);
    }

    /// <summary>
    /// The external link. The url is stored as typed — <see cref="EntryLinkUrl.Normalize"/> is only
    /// for comparing — so a member's url is not quietly rewritten into one they did not paste.
    /// </summary>
    private EntryLink ExternalLink(PostEntryLinkRequest req, Entry entry, Member member)
    {
        var url = (req.Url ?? "").Trim();
        var label = (req.Label ?? "").Trim();
        var normalized = EntryLinkUrl.Normalize(url);

        if (entry.Links.Any(l => l.Kind == EntryLinkKind.External && EntryLinkUrl.Normalize(l.Url) == normalized))
        {
            ThrowError(
                new ValidationFailure(UrlErrorKey, "That link is already on this entry."),
                StatusCodes.Status409Conflict);
        }

        return New(EntryLinkKind.External, null, null, url, label, member);
    }

    private static EntryLink New(EntryLinkKind kind, string? provider, string? itemId, string? url, string? label, Member member)
        => new(
            Id: Guid.NewGuid(),
            Kind: kind,
            Provider: provider,
            ItemId: itemId,
            Url: url,
            Label: label,
            // Truncated like a quote's PromotedAt, so the value the projection orders by equals
            // itself after a round trip through the document's JSON.
            AddedAt: Microseconds.Truncate(DateTimeOffset.UtcNow),
            AddedByMemberId: member.MemberId);
}
