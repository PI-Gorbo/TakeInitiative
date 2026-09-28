using FastEndpoints;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.LooseEnds;

public record GetLooseEndCountsRequest
{
    public Guid CampaignId { get; init; }
}

public record LooseEndCountsResponse
{
    /// <summary>Every loose end of the caller: the Wiki's "Loose ends (n)" and ⌘K's.</summary>
    public required int Total { get; init; }
    /// <summary>Per session id, the loose ends that belong to it: a divider's 🧵 n. Sessions with none are absent.</summary>
    public required Dictionary<Guid, int> BySession { get; init; }
}

/// <summary>
/// The caller's loose ends counted (19b.4): the same rule as <c>GET loose-ends</c>
/// (<see cref="LooseEnds.For"/>), grouped, with no matcher call, so the web can refetch it on
/// every push. An entry with no session counts in <c>total</c> only.
/// </summary>
public class GetLooseEndCounts(IDocumentSession session) : Endpoint<GetLooseEndCountsRequest, LooseEndCountsResponse>
{
    public override void Configure()
    {
        Get("/api/campaigns/{CampaignId}/loose-ends/counts");
    }

    public override async Task HandleAsync(GetLooseEndCountsRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);

        var read = await LooseEnds.For(session, req.CampaignId, member, ct);

        await SendAsync(new LooseEndCountsResponse
        {
            Total = read.Items.Count,
            BySession = read.Items
                .Where(i => i.SessionId is not null)
                .GroupBy(i => i.SessionId!.Value)
                .ToDictionary(g => g.Key, g => g.Count()),
        }, cancellation: ct);
    }
}
