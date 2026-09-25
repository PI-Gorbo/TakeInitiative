using FastEndpoints;
using Marten;
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
                    };
                })
                .ToArray(),
        }, cancellation: ct);
    }
}
