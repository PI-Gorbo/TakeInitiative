using FastEndpoints;
using FluentValidation;

using Marten;

using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Reference.KnowledgeBase;
using TakeInitiative.Api.Features.Search;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Reference;

public record GetKnowledgeBaseRequest
{
    /// <summary>
    /// The campaign whose membership authorises the call. It filters nothing: the knowledge base is
    /// global, and every member of every campaign sees the same rows.
    /// </summary>
    public Guid CampaignId { get; init; }

    /// <summary>The search text, matched against names on the same ladder as ⌘K. Omit it for a plain list by name.</summary>
    public string? Q { get; init; }

    /// <summary>One category, or omit it for all three.</summary>
    public ReferenceCategory? Category { get; init; }

    /// <summary>One source book's abbreviation (<c>MM</c>), or omit it for every book.</summary>
    public string? Book { get; init; }

    /// <summary>One corpus's key (<c>5etools</c>), or omit it for every corpus.</summary>
    public string? Provider { get; init; }

    /// <summary>How many rows to step over. 0 by default.</summary>
    public int? Skip { get; init; }

    /// <summary>How many rows to answer: <see cref="GetKnowledgeBase.DefaultTake" /> by default, at most <see cref="GetKnowledgeBase.MaxTake" />.</summary>
    public int? Take { get; init; }
}

public class GetKnowledgeBaseRequestValidator : Validator<GetKnowledgeBaseRequest>
{
    public GetKnowledgeBaseRequestValidator()
    {
        RuleFor(x => x.Take).InclusiveBetween(1, GetKnowledgeBase.MaxTake);
        RuleFor(x => x.Skip).InclusiveBetween(0, GetKnowledgeBase.MaxSkip);
        RuleFor(x => x.Q).MaximumLength(SearchQuery.MaxLength);
        RuleFor(x => x.Book).MaximumLength(GetKnowledgeBase.MaxBookLength);
        RuleFor(x => x.Provider).MaximumLength(GetKnowledgeBase.MaxProviderLength);
    }
}

/// <summary>
/// The knowledge base's browse list (26e): a page of ingested reference rows, filtered by category,
/// source book and corpus, optionally searched, with the total and the counts the filters show beside
/// their values.
/// </summary>
/// <remarks>
/// <para>
/// <b>The campaign in the route is there for membership and nothing else.</b> The knowledge base has
/// no <c>CampaignId</c> and no visibility rules — reference content is the same for every campaign and
/// every member, which <c>IReferenceProvider</c> already states — so the campaign filters no rows. It
/// is in the route because a page under <c>/app/campaigns/{id}/knowledge-base</c> should be reachable
/// by a member of that campaign and by nobody else, and <see cref="CampaignAccess.RequireMember" /> is
/// how every other campaign-scoped endpoint says so: a missing campaign is a 404 and a non-member is a
/// 403.
/// </para>
/// <para>
/// <b>An empty table is a 200 with a total of zero</b>, not a 404 and not an error. That is the state
/// step 21 could never report: <c>IndexPath</c> being unset made the provider vanish silently, and
/// there was no surface that could say "nothing has been ingested yet". 26f's page reads
/// <see cref="KnowledgeBaseResponse.Total" /> of zero with no filters set as exactly that.
/// </para>
/// <para>
/// <b>It is cached like the item endpoint.</b> The rows change only when an operator runs the ingest,
/// so a private day-long cache is safe and makes paging back and forth free. It is <c>private</c>
/// because the response is behind a membership check, not because its content is.
/// </para>
/// </remarks>
public class GetKnowledgeBase(IDocumentSession session, KnowledgeBaseQueries queries, ReferenceCatalog catalog)
    : Endpoint<GetKnowledgeBaseRequest, KnowledgeBaseResponse>
{
    public const int DefaultTake = 30;

    /// <summary>The cap the plan sets. A browse page loads more rather than asking for hundreds.</summary>
    public const int MaxTake = 50;

    /// <summary>
    /// The furthest a caller may page. A hundred thousand rows is an order of magnitude past any real
    /// corpus, and an unbounded <c>OFFSET</c> is a scan the caller chooses the length of.
    /// </summary>
    public const int MaxSkip = 100_000;

    /// <summary>A source book is an abbreviation, and the longest 5eTools uses is well inside this.</summary>
    public const int MaxBookLength = 40;

    /// <summary>A provider key is a word.</summary>
    public const int MaxProviderLength = 40;

    /// <summary>
    /// The corpus changes only on an ingest, but it must be allowed to change <em>then</em>. A
    /// day-long <c>max-age</c> meant a browser that had seen the corpus could not see a re-ingest
    /// for a day without a hard reload — and "ingest, then look at it" is this page's whole
    /// purpose, so the one workflow it has was the one the cache broke.
    /// <para>
    /// <c>no-cache</c> still lets the response be stored; it requires revalidation before reuse.
    /// A page is at most <see cref="MaxTake"/> rows, so re-sending one is cheap. An <c>ETag</c>
    /// over the provider's latest <c>ingested_at</c> would make revalidation a 304 instead, which
    /// is the cheap improvement if this ever shows up in a profile.
    /// </para>
    /// </summary>
    public const string CacheControl = "private, no-cache";

    public override void Configure()
    {
        Get("/api/campaigns/{CampaignId}/knowledge-base");
    }

    public override async Task HandleAsync(GetKnowledgeBaseRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        await this.RequireMember(session, req.CampaignId, userId, ct);

        var (page, facets) = await queries.BrowseAsync(
            new KnowledgeBaseBrowse(
                Provider: req.Provider,
                Category: req.Category,
                Book: req.Book,
                Query: req.Q,
                Skip: req.Skip ?? 0,
                Take: req.Take ?? DefaultTake),
            ct);

        HttpContext.Response.Headers.CacheControl = CacheControl;
        await SendAsync(new KnowledgeBaseResponse
        {
            Items = [.. page.Items.Select(row => KnowledgeBaseItemResponse.From(row, catalog.Get(row.Provider)?.Label))],
            Total = page.Total,
            Facets = new KnowledgeBaseFacetsResponse
            {
                Categories =
                [
                    .. facets.Categories.Select(facet => new KnowledgeBaseCategoryFacetResponse
                    {
                        Category = Enum.Parse<ReferenceCategory>(facet.Value),
                        Count = facet.Count,
                    }),
                ],
                Books =
                [
                    .. facets.Books.Select(facet => new KnowledgeBaseBookFacetResponse
                    {
                        Book = facet.Value,
                        BookTitle = facet.Title,
                        Count = facet.Count,
                    }),
                ],
            },
        }, cancellation: ct);
    }
}
