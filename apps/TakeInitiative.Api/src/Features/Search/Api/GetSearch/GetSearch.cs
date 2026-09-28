using FastEndpoints;
using FluentValidation.Results;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Search;

public record GetSearchRequest
{
    public Guid CampaignId { get; init; }
    /// <summary>
    /// What to search for, 1 to 100 characters after the prefix. A leading <c>@</c> searches
    /// entries only. There are no search operators: quotes, <c>&amp;</c>, <c>|</c>, <c>!</c>,
    /// <c>:</c> and <c>*</c> are plain text.
    /// </summary>
    public string? Q { get; init; }
    /// <summary>A comma list of <c>entries,notes,images,sessions,combats,reference</c>. All of them by default.</summary>
    public string? Sections { get; init; }
    /// <summary>How many hits each section returns: 5 by default, at most 20.</summary>
    public int? Take { get; init; }
}

/// <summary>
/// ⌘K search (17a.11): runs the providers for the caller and answers the sections with their
/// hits and snippets. Visibility is applied inside the SQL, down to secret blocks, so a note,
/// entry or block outside the caller's audience adds no hit, no snippet text, no count and no
/// change of rank (invariant 5).
/// <para>
/// The query string is never stored, and it is not pushed anywhere. A search is a moment.
/// </para>
/// </summary>
public class GetSearch(IDocumentSession session, SearchService search) : Endpoint<GetSearchRequest, SearchResponse>
{
    public const int DefaultTake = 5;
    public const int MaxTake = 20;

    public const string QueryErrorKey = "q";
    public const string SectionsErrorKey = "sections";
    public const string TakeErrorKey = "take";

    public override void Configure()
    {
        Get("/api/campaigns/{CampaignId}/search");
    }

    public override async Task HandleAsync(GetSearchRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        // Membership first: an outsider gets a 403 and never learns whether their query was valid.
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);

        var query = SearchQuery.Parse(req.Q);
        if (query is null)
        {
            ThrowError(new ValidationFailure(QueryErrorKey, SearchQuery.LengthError), StatusCodes.Status400BadRequest);
        }

        var take = req.Take ?? DefaultTake;
        if (take is < 1 or > MaxTake)
        {
            ThrowError(new ValidationFailure(TakeErrorKey, $"Take between 1 and {MaxTake} hits."), StatusCodes.Status400BadRequest);
        }

        var sections = await search.SearchAsync(
            query,
            new SearchContext(session, req.CampaignId, member, Wanted(query, req.Sections), take),
            ct);

        await SendAsync(new SearchResponse { Query = query.Text, Sections = sections }, cancellation: ct);
    }

    /// <summary>
    /// The sections to fill: every one by default, the named ones when <c>sections</c> is given,
    /// and Entries alone for an <c>@</c> query whatever was asked for — the prefix is the narrower
    /// instruction.
    /// </summary>
    private IReadOnlySet<SearchSectionKey> Wanted(SearchQuery query, string? sections)
    {
        if (query.Scope == SearchScope.Entries)
        {
            return new HashSet<SearchSectionKey> { SearchSectionKey.Entries };
        }
        if (string.IsNullOrWhiteSpace(sections))
        {
            return Enum.GetValues<SearchSectionKey>().ToHashSet();
        }

        var wanted = new HashSet<SearchSectionKey>();
        foreach (var name in sections.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // A section is named, never numbered: Enum.TryParse also takes the numbers behind the
            // names, so "0" would quietly mean entries and "99" would be a section that does not
            // exist, answered with 200 and an empty list. The parsed value has to spell its name
            // back for the name to count (17a.11).
            if (!Enum.TryParse<SearchSectionKey>(name, ignoreCase: true, out var key)
                || !string.Equals(Enum.GetName(key), name, StringComparison.OrdinalIgnoreCase))
            {
                ThrowError(
                    new ValidationFailure(SectionsErrorKey,
                        $"Search sections are {string.Join(", ", Enum.GetNames<SearchSectionKey>()).ToLowerInvariant()}."),
                    StatusCodes.Status400BadRequest);
            }
            wanted.Add(key);
        }
        if (wanted.Count == 0)
        {
            ThrowError(new ValidationFailure(SectionsErrorKey, "Name at least one section."), StatusCodes.Status400BadRequest);
        }
        return wanted;
    }
}
