using FastEndpoints;
using FluentValidation.Results;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public record PostCombatRollRequest
{
    public Guid CampaignId { get; init; }
    public Guid CombatId { get; init; }
}

/// <summary>
/// Rolls initiative (18b.1) with one <see cref="InitiativeRolled"/>.
/// <list type="bullet">
/// <item>A DM rolls every waiting combatant. A player rolls only their own waiting ones, and
/// only in an Active combat (a player never sees a Draft).</item>
/// <item>The first roll starts the combat: a Draft becomes Active in round 1, moves to the
/// current session, and the turn goes to the top of the order. It starts with no combatants
/// too.</item>
/// <item>Later rolls slot the late joiners in by sort, without re-rolling anyone, and the turn
/// stays where it was. With nothing waiting, it is a 200 that appends nothing.</item>
/// </list>
/// </summary>
public class PostCombatRoll(IDocumentSession session, IHubContext<CampaignHub> hub, IDiceRoller dice)
    : Endpoint<PostCombatRollRequest, CombatResponse>
{
    public const string RollErrorKey = "roll";

    public override void Configure()
    {
        Post("/api/campaigns/{CampaignId}/combats/{CombatId}/roll");
    }

    public override async Task HandleAsync(PostCombatRollRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (campaign, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var current = await session.CurrentSession(req.CampaignId, ct);

        await this.Write(session, hub, campaign, req.CombatId, member, combat =>
        {
            var starting = combat.Status == CombatStatus.Draft;
            if (starting && member.Role != Role.DM)
            {
                // Unreachable: a player cannot see a Draft, so Write has already answered 404.
                ThrowError("Only a DM can start a combat.", StatusCodes.Status403Forbidden);
            }

            var waiting = CombatOrder.Waiting(combat)
                .Where(c => member.Role == Role.DM || c.OwnerMemberId == member.MemberId)
                .ToList();
            if (waiting.Count == 0 && !starting)
            {
                return [];
            }

            var rolls = new List<InitiativeRollResult>();
            foreach (var combatant in waiting)
            {
                var rolled = dice.EvaluateRoll(combatant.InitiativeRoll);
                if (rolled.IsFailure)
                {
                    // Every expression was checked when it was stored, so this is a bad
                    // expression, not a server fault.
                    ThrowError(new ValidationFailure(RollErrorKey, $"{combatant.Name}: {rolled.Error}"));
                }
                var total = Math.Clamp(rolled.Value.Total, Combatant.InitiativeMin, Combatant.InitiativeMax);
                rolls.Add(new InitiativeRollResult(combatant.Id, total, rolled.Value.Roll, rolled.Value.Evaluation));
            }

            return [new InitiativeRolled(Actor.Member(member.MemberId), starting ? current?.Id : null, [.. rolls])];
        }, ct);
    }
}
