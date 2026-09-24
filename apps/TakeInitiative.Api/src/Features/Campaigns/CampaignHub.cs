using CSharpFunctionalExtensions;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Campaigns;

public static class CampaignHubContextExtensions
{
    public static Task NotifyCampaignStateUpdated(this IHubContext<CampaignHub> hubContext, Guid campaignId)
    {
        return hubContext.Clients.Group(campaignId.ToString())
            .SendAsync("campaignStateUpdated");
    }

    public static Task NotifyCampaignMemberStateUpdated(this IHubContext<CampaignHub> hubContext, CampaignMember campaignMember)
    {
        return hubContext.Clients.Group(campaignMember.CampaignId.ToString())
            .SendAsync("campaignMemberStateUpdated", campaignMember);
    }
}


[Authorize] // TakePolicies.UserExists
public class CampaignHub : Hub
{
    public async Task Join(IDocumentSession session, Guid CampaignId)
    {
        // The caller's id comes from the authenticated connection, never from the client.
        Result<Guid> callerUserId = Context.User.GetUserId();
        if (callerUserId.IsFailure)
        {
            throw new OperationCanceledException(callerUserId.Error);
        }

        // Ensure the caller is a member of the campaign by finding the campaign member entity for them.
        Result joinHubResult = await Result
            .Try(async () => await session.UserIsApartOfCampaign(callerUserId.Value, CampaignId))
            .Ensure(memberExists => memberExists, "The user must be part of the campaign to join the hub.")
            .TapTry(async () =>
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, CampaignId.ToString());
            });

        if (joinHubResult.IsFailure)
        {
            throw new OperationCanceledException(joinHubResult.Error);
        }

        return;
    }

    public async Task Leave(Guid CampaignId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, CampaignId.ToString());
    }
}
