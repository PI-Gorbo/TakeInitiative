using FastEndpoints;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Connections;

public record GetEntryConnectionsRequest
{
    public Guid CampaignId { get; init; }
    public Guid EntryId { get; init; }
}

public record EntryConnectionsResponse
{
    /// <summary>Heaviest first, then the most recent, then by name.</summary>
    public required EntryConnectionResponse[] Connections { get; init; }
}

/// <summary>One connection of an entry, as the viewer sees it (glossary: Connection).</summary>
public record EntryConnectionResponse
{
    /// <summary>The other end.</summary>
    public required EntrySummaryResponse Entry { get; init; }
    /// <summary><see cref="Notes"/> + <see cref="Blocks"/> + <see cref="Combats"/>: the evidence rows the viewer gets for this pair.</summary>
    public required int Weight { get; init; }
    public required int Notes { get; init; }
    public required int Blocks { get; init; }
    /// <summary>Combats both fought in (glossary: Fought together).</summary>
    public required int Combats { get; init; }
    /// <summary>The newest note's <c>postedAt</c> or combat's <c>startedAt</c>; null when only article blocks connect them.</summary>
    public DateTimeOffset? LastAt { get; init; }
}

/// <summary>
/// An entry's connections (19a.3, design §6): the other entries that co-occur with it in a note,
/// an article block or a started combat the caller can see, with the weight of each. Computed on
/// every read from what the caller can see and never stored, so it cannot leak what they cannot
/// (invariant 7). An entry the caller cannot see is a 404; a merged id answers for its target.
/// </summary>
public class GetEntryConnections(IDocumentSession session) : Endpoint<GetEntryConnectionsRequest, EntryConnectionsResponse>
{
    public override void Configure()
    {
        Get("/api/campaigns/{CampaignId}/entries/{EntryId}/connections");
    }

    public override async Task HandleAsync(GetEntryConnectionsRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var entry = await this.RequireVisibleEntry(session, req.CampaignId, req.EntryId, member, ct);

        var read = await ConnectionIndex.ForEntry(session, req.CampaignId, entry, member, ct);

        await SendAsync(new EntryConnectionsResponse
        {
            Connections = read.Pairs.Values
                .Where(c => c.Pair.Contains(entry.Id))
                .Select(c => (Connection: c, Other: read.Entries[c.Pair.Other(entry.Id)]))
                .OrderByDescending(x => x.Connection.Weight)
                .ThenByDescending(x => x.Connection.LastAt ?? DateTimeOffset.MinValue)
                .ThenBy(x => x.Other.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => new EntryConnectionResponse
                {
                    Entry = EntrySummaryResponse.From(x.Other),
                    Weight = x.Connection.Weight,
                    Notes = x.Connection.Notes,
                    Blocks = x.Connection.Blocks,
                    Combats = x.Connection.Combats,
                    LastAt = x.Connection.LastAt,
                })
                .ToArray(),
        }, cancellation: ct);
    }
}
