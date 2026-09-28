using System.Net;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public record PutCombatantPositionRequest
{
    public Guid CampaignId { get; init; }
    public Guid CombatId { get; init; }
    public Guid CombatantId { get; init; }
    /// <summary>The combatant it goes just after; null for the top of the order.</summary>
    public Guid? AfterId { get; init; }
}

/// <summary>
/// A DM drags a combatant to a new place in the initiative order (18b.4): just after
/// <see cref="PutCombatantPositionRequest.AfterId"/>, or to the top when it is null. The server
/// picks its initiative and tiebreak (<see cref="CombatOrder.Place"/>), respacing that
/// initiative's ties when there is no room, and appends one <see cref="CombatantEdited"/> per
/// combatant that changed. The turn stays with whoever has it.
/// <para>
/// Waiting combatants cannot be placed this way, as the one moved or as the one it goes
/// after (400): roll them or type an initiative. Dropping a combatant where it already is, or
/// after itself, appends nothing.
/// </para>
/// </summary>
public class PutCombatantPosition(IDocumentSession session, IHubContext<CampaignHub> hub) : Endpoint<PutCombatantPositionRequest, CombatResponse>
{
    public const string WaitingMessage = "A waiting combatant has no place in the order yet. Roll it or type an initiative.";

    public override void Configure()
    {
        Put("/api/campaigns/{CampaignId}/combats/{CombatId}/combatants/{CombatantId}/position");
    }

    public override async Task HandleAsync(PutCombatantPositionRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (campaign, member) = await this.RequireMember(session, req.CampaignId, userId, ct);

        await this.Write(session, hub, campaign, req.CombatId, member, combat =>
        {
            this.RequireDm(member, "Only a DM can reorder the combat.");
            var moved = Placed(combat, req.CombatantId);
            if (req.AfterId is { } afterId)
            {
                if (afterId == moved.Id)
                {
                    return [];
                }
                Placed(combat, afterId);
            }

            var actor = Actor.Member(member.MemberId);
            return CombatOrder.Place(CombatOrder.Ordered(combat), moved.Id, req.AfterId)
                .Select(p => (object)new CombatantEdited(
                    actor,
                    p.CombatantId,
                    combat.Find(p.CombatantId)!.State with { Initiative = p.Initiative },
                    p.Tiebreak))
                .ToList();
        }, ct);
    }

    private Combatant Placed(Combat combat, Guid combatantId)
    {
        var combatant = combat.Find(combatantId);
        if (combatant is null)
        {
            ThrowError("There is no combatant with the given id.", (int)HttpStatusCode.NotFound);
        }
        if (combatant.IsWaiting)
        {
            ThrowError(WaitingMessage, (int)HttpStatusCode.BadRequest);
        }
        return combatant;
    }
}
