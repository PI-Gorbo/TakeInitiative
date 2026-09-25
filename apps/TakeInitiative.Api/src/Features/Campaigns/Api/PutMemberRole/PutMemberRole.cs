using System.Net;
using FastEndpoints;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Campaigns;

public record PutMemberRoleRequest
{
    public Guid CampaignId { get; init; }
    public Guid MemberId { get; init; }
    public required Role Role { get; init; }
}

public class PutMemberRoleRequestValidator : Validator<PutMemberRoleRequest>
{
    public PutMemberRoleRequestValidator()
    {
        RuleFor(x => x.Role).IsInEnum();
    }
}

/// <summary>The owner changes a member's role. The owner can never be demoted.</summary>
public class PutMemberRole(IDocumentSession session, IHubContext<CampaignHub> hub, CampaignConnections connections)
    : Endpoint<PutMemberRoleRequest, CampaignResponse>
{
    public override void Configure()
    {
        Put("/api/campaigns/{CampaignId}/members/{MemberId}/role");
    }

    public override async Task HandleAsync(PutMemberRoleRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();

        var stream = await session.Events.FetchForWriting<Campaign>(req.CampaignId, ct);
        var campaign = stream.Aggregate;
        if (campaign is null)
        {
            ThrowError("There is no campaign with the given id.", (int)HttpStatusCode.NotFound);
        }

        // Resolve the caller's member first: a non-member gets 403 before anything is appended.
        var caller = campaign.MemberForUser(userId);
        if (caller is null)
        {
            ThrowError("You are not a member of this campaign.", (int)HttpStatusCode.Forbidden);
        }
        if (!campaign.IsOwner(caller.MemberId))
        {
            ThrowError("Only the owner of the campaign can change roles.", (int)HttpStatusCode.Forbidden);
        }

        var target = campaign.MemberById(req.MemberId);
        if (target is null)
        {
            ThrowError("There is no member with the given id in this campaign.", (int)HttpStatusCode.NotFound);
        }
        if (campaign.IsOwner(target.MemberId) && req.Role != Role.DM)
        {
            ThrowError(r => r.Role, "The owner of the campaign cannot be demoted.");
        }

        if (target.Role != req.Role)
        {
            stream.AppendOne(new MemberRoleChanged(Actor.Member(caller.MemberId), target.MemberId, req.Role));
            await session.SaveChangesAsync(ct);

            campaign = (await session.LoadAsync<Campaign>(campaign.Id, ct))!;
            await hub.NotifyMemberRoleChanged(connections, campaign.Id, target.MemberId, req.Role);
        }

        await SendAsync(await CampaignResponse.Build(session, campaign, userId, ct), cancellation: ct);
    }
}
