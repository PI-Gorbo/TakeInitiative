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
    public required EntrySummaryResponse[] Entries { get; init; }
}

/// <summary>The wiki's directory: every entry the caller can see, with the visibility rule in the SQL.</summary>
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
            .Where(e => e.CampaignId == req.CampaignId)
            .Where(EntryVisibility.VisibleTo(member))
            .OrderBy(e => e.Name)
            .ToListAsync(ct);

        await SendAsync(new GetEntriesResponse
        {
            Entries = entries.Select(EntrySummaryResponse.From).ToArray(),
        }, cancellation: ct);
    }
}
