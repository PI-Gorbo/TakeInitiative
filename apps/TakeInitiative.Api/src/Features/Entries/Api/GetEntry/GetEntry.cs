using FastEndpoints;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Entries;

public record GetEntryRequest
{
    public Guid CampaignId { get; init; }
    public Guid EntryId { get; init; }
}

/// <summary>One entry. An entry the caller cannot see is a 404.</summary>
public class GetEntry(IDocumentSession session) : Endpoint<GetEntryRequest, EntryResponse>
{
    public override void Configure()
    {
        Get("/api/campaigns/{CampaignId}/entries/{EntryId}");
    }

    public override async Task HandleAsync(GetEntryRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var entry = await this.RequireVisibleEntry(session, req.CampaignId, req.EntryId, member, ct);

        await SendAsync(EntryResponse.From(entry), cancellation: ct);
    }
}
