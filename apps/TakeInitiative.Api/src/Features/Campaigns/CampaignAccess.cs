using System.Net;
using FastEndpoints;
using Marten;

namespace TakeInitiative.Api.Features.Campaigns;

public static class CampaignAccess
{
    /// <summary>
    /// Loads the campaign and resolves the caller's member in it. A missing campaign is
    /// a 404 and a non-member is a 403, so nothing past this call runs for an outsider.
    /// </summary>
    public static async Task<(Campaign Campaign, Member Member)> RequireMember<TRequest, TResponse>(
        this Endpoint<TRequest, TResponse> endpoint, IQuerySession session, Guid campaignId, Guid userId, CancellationToken ct)
        where TRequest : notnull
    {
        var campaign = await session.LoadAsync<Campaign>(campaignId, ct);
        if (campaign is null)
        {
            endpoint.ThrowError("There is no campaign with the given id.", (int)HttpStatusCode.NotFound);
        }

        var member = campaign.MemberForUser(userId);
        if (member is null)
        {
            endpoint.ThrowError("You are not a member of this campaign.", (int)HttpStatusCode.Forbidden);
        }

        return (campaign, member);
    }
}
