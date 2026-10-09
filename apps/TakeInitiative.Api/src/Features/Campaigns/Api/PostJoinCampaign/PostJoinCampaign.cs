using FastEndpoints;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Campaigns;

public record PostJoinCampaignRequest
{
    public required string JoinCode { get; init; }
}

public class PostJoinCampaignRequestValidator : Validator<PostJoinCampaignRequest>
{
    public PostJoinCampaignRequestValidator()
    {
        RuleFor(x => x.JoinCode).NotEmpty();
    }
}

/// <summary>Joins a campaign by join code, as a Player. Joining a campaign you are already in is a no-op.</summary>
public class PostJoinCampaign(IDocumentSession session, IHubContext<CampaignHub> hub)
    : Endpoint<PostJoinCampaignRequest, CampaignResponse>
{
    public override void Configure()
    {
        Post("/api/campaigns/join");
    }

    public override async Task HandleAsync(PostJoinCampaignRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var joinCode = Campaign.NormaliseJoinCode(req.JoinCode);

        var found = await session.Query<Campaign>().FirstOrDefaultAsync(c => c.JoinCode == joinCode, ct);
        if (found is null)
        {
            ThrowError(r => r.JoinCode, "Join code is invalid.");
        }

        // FetchForWriting gives optimistic concurrency on the stream for the append.
        var stream = await session.Events.FetchForWriting<Campaign>(found.Id, ct);
        var campaign = stream.Aggregate!;
        if (campaign.MemberForUser(userId) is null)
        {
            var memberId = Guid.NewGuid();
            stream.AppendOne(new MemberJoined(Actor.Member(memberId), memberId, userId));
            await session.SaveChangesAsync(ct);

            campaign = (await session.LoadAsync<Campaign>(campaign.Id, ct))!;
            await hub.NotifyMemberJoined(campaign.Id, memberId);
        }

        await SendAsync(await CampaignResponse.Build(session, campaign, userId, ct), cancellation: ct);
    }
}
