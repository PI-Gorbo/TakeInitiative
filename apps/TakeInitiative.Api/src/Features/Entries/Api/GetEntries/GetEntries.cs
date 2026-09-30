using FastEndpoints;
using Marten;
using TakeInitiative.Api.Features.Sessions;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Entries;

public record GetEntriesRequest
{
    public Guid CampaignId { get; init; }
}

public record GetEntriesResponse
{
    /// <summary>Every entry the caller can see, by name. Hidden entries are absent, not marked.</summary>
    public required EntryListItemResponse[] Entries { get; init; }
}

/// <summary>
/// An entry in the wiki's list, with how often the notes and article blocks the caller can
/// see mention it, and how recently the notes do. The counts are per viewer and never
/// pushed: a count over notes or blocks the viewer cannot see would reveal that they exist.
/// </summary>
public record EntryListItemResponse
{
    public required EntrySummaryResponse Entry { get; init; }
    /// <summary>
    /// How many notes and article blocks (15e) the caller can see mention the entry. A note or
    /// block that mentions it twice counts once, and its own article does not count.
    /// </summary>
    public required int MentionCount { get; init; }
    /// <summary>When the latest of those notes was posted. Null when no note mentions it (blocks have no time).</summary>
    public DateTimeOffset? LastMentionedAt { get; init; }
    /// <summary>How many of those are notes (25g): "N notes", and "N notes to pick from" for an empty summary.</summary>
    public required int NoteCount { get; init; }
    /// <summary>The number of the session the latest of those notes is in (25g): "last in Session X".</summary>
    public int? LastMentionedSessionNumber { get; init; }
    /// <summary>
    /// The article's first line as the caller sees it, as plain text (<see cref="ArticleGist"/>),
    /// never from a secret block. Null when there is none. Per viewer, so it is never pushed.
    /// </summary>
    public string? SummaryGist { get; init; }
}

/// <summary>
/// The wiki's directory: every entry the caller can see, with the visibility rule in the SQL,
/// and its mention counts from <see cref="MentionIndex.CountsFor"/>.
/// </summary>
public class GetEntries(IDocumentSession session) : Endpoint<GetEntriesRequest, GetEntriesResponse>
{
    public override void Configure()
    {
        Get("/api/campaigns/{CampaignId}/entries");
    }

    public override async Task HandleAsync(GetEntriesRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);

        var entries = await session.Query<Entry>()
            .Listed(req.CampaignId, member)
            .OrderBy(e => e.Name)
            .ToListAsync(ct);
        var counts = await MentionIndex.CountsFor(session, req.CampaignId, member, ct, entries);
        var sessionIds = counts.Values.Select(c => c.LastSessionId).OfType<Guid>().Distinct().ToArray();
        var sessionNumbers = sessionIds.Length == 0
            ? new Dictionary<Guid, int>()
            : (await session.Query<Session>()
                .Where(s => s.CampaignId == req.CampaignId && s.Id.IsOneOf(sessionIds))
                .Select(s => new { s.Id, s.Number })
                .ToListAsync(ct))
                .ToDictionary(s => s.Id, s => s.Number);

        await SendAsync(new GetEntriesResponse
        {
            Entries = entries
                .Select(e =>
                {
                    var count = counts.GetValueOrDefault(e.Id);
                    return new EntryListItemResponse
                    {
                        Entry = EntrySummaryResponse.From(e),
                        MentionCount = count?.Count ?? 0,
                        LastMentionedAt = count?.LastMentionedAt,
                        NoteCount = count?.NoteCount ?? 0,
                        LastMentionedSessionNumber = count?.LastSessionId is { } sessionId
                            && sessionNumbers.TryGetValue(sessionId, out var number) ? number : null,
                        SummaryGist = ArticleGist.For(e, member),
                    };
                })
                .ToArray(),
        }, cancellation: ct);
    }
}
