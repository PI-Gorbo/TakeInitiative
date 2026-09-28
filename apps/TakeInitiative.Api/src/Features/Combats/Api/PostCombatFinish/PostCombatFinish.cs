using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public record PostCombatFinishRequest
{
    public Guid CampaignId { get; init; }
    public Guid CombatId { get; init; }
}

/// <summary>
/// A DM finishes a combat (18b.5) with one <see cref="CombatFinished"/>: it becomes read-only,
/// with no turn. From <c>Active</c>, and from <c>Draft</c> as the way to discard one: a combat
/// that never started stays DM-only, so a discarded Draft shows only in a DM's list. Finishing
/// an empty combat succeeds. Finishing twice is the Finished 409.
/// </summary>
public class PostCombatFinish(IDocumentSession session, IHubContext<CampaignHub> hub) : Endpoint<PostCombatFinishRequest, CombatResponse>
{
    public override void Configure()
    {
        Post("/api/campaigns/{CampaignId}/combats/{CombatId}/finish");
    }

    public override async Task HandleAsync(PostCombatFinishRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (campaign, member) = await this.RequireMember(session, req.CampaignId, userId, ct);

        await this.Write(session, hub, campaign, req.CombatId, member, combat =>
        {
            this.RequireDm(member, "Only a DM can finish a combat.");
            return [new CombatFinished(Actor.Member(member.MemberId))];
        }, ct);
    }
}
