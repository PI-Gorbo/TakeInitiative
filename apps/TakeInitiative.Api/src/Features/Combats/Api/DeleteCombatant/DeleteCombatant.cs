using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public record DeleteCombatantRequest
{
    public Guid CampaignId { get; init; }
    public Guid CombatId { get; init; }
    public Guid CombatantId { get; init; }
}

/// <summary>
/// Removes a combatant: DMs any, a player their own. When it had the turn, the turn passes to
/// the next in the order. The response is the caller's view of the combat after it.
/// </summary>
public class DeleteCombatant(IDocumentSession session, IHubContext<CampaignHub> hub) : Endpoint<DeleteCombatantRequest, CombatResponse>
{
    public override void Configure()
    {
        Delete("/api/campaigns/{CampaignId}/combats/{CombatId}/combatants/{CombatantId}");
    }

    public override async Task HandleAsync(DeleteCombatantRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (campaign, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        await this.Write(session, hub, campaign, req.CombatId, member, combat =>
        {
            var combatant = this.RequireChangeableCombatant(combat, req.CombatantId, member);
            return [new CombatantRemoved(Actor.Member(member.MemberId), combatant.Id)];
        }, ct);
    }
}
