using System.Net;
using FastEndpoints;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Campaigns;

public record GetCampaignRequest
{
    public Guid CampaignId { get; init; }
}

/// <summary>The campaign with its members, their usernames and roles. Members only.</summary>
public class GetCampaign(IDocumentSession session) : Endpoint<GetCampaignRequest, CampaignResponse>
{
    public override void Configure()
    {
        Get("/api/campaigns/{CampaignId}");
    }

    public override async Task HandleAsync(GetCampaignRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var campaign = await session.LoadAsync<Campaign>(req.CampaignId, ct);
        if (campaign is null)
        {
            ThrowError("There is no campaign with the given id.", (int)HttpStatusCode.NotFound);
        }
        if (campaign.MemberForUser(userId) is null)
        {
            ThrowError("You are not a member of this campaign.", (int)HttpStatusCode.Forbidden);
        }

        await SendAsync(await CampaignResponse.Build(session, campaign, userId, ct), cancellation: ct);
    }
}
