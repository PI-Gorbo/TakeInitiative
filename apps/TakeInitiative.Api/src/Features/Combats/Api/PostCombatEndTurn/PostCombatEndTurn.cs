using FastEndpoints;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public record PostCombatEndTurnRequest
{
    public Guid CampaignId { get; init; }
    public Guid CombatId { get; init; }
    /// <summary>Whose turn the caller is ending.</summary>
    public required Guid CombatantId { get; init; }
    /// <summary>The round the caller saw it in.</summary>
    public required int Round { get; init; }
}

public class PostCombatEndTurnRequestValidator : Validator<PostCombatEndTurnRequest>
{
    public PostCombatEndTurnRequestValidator()
    {
        RuleFor(x => x.CombatantId).NotEmpty().WithMessage("Say whose turn is ending.");
        RuleFor(x => x.Round).GreaterThanOrEqualTo(1).WithMessage("A round starts at 1.");
    }
}

/// <summary>
/// Ends a turn (18b.2) with one <see cref="TurnEnded"/>: the turn moves to the next combatant in
/// the order, and past the last one it wraps to the top and the round goes up. Hidden
/// combatants take turns like any other.
/// <list type="bullet">
/// <item>The body says whose turn, and in which round, the caller is ending. When that is no
/// longer the turn (someone else ended it first), it is a 409 "The turn has already moved
/// on.", which the web treats as done: a double tap or two DMs at once end one turn.</item>
/// <item>DMs end any turn; a player ends the turn of a combatant they own (403 otherwise). A
/// combatant the player cannot see answers the same 409 as a stale turn, so a hidden
/// combatant's turn does not leak.</item>
/// <item>A Draft has no turn: 409.</item>
/// </list>
/// </summary>
public class PostCombatEndTurn(IDocumentSession session, IHubContext<CampaignHub> hub) : Endpoint<PostCombatEndTurnRequest, CombatResponse>
{
    public const string MovedOnMessage = "The turn has already moved on.";

    public override void Configure()
    {
        Post("/api/campaigns/{CampaignId}/combats/{CombatId}/end-turn");
    }

    public override async Task HandleAsync(PostCombatEndTurnRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (campaign, member) = await this.RequireMember(session, req.CampaignId, userId, ct);

        await this.Write(session, hub, campaign, req.CombatId, member, combat =>
        {
            if (combat.Status == CombatStatus.Draft)
            {
                ThrowError("This combat hasn't started yet.", StatusCodes.Status409Conflict);
            }

            var combatant = combat.Find(req.CombatantId);
            var visible = combatant is not null && CombatView.CanSee(combatant, member);
            if (visible && !CombatAccess.CanChange(combatant!, member))
            {
                ThrowError("Players can only end their own turn.", StatusCodes.Status403Forbidden);
            }
            if (!visible || combat.TurnCombatantId != req.CombatantId || combat.Round != req.Round)
            {
                ThrowError(MovedOnMessage, StatusCodes.Status409Conflict);
            }

            var (next, wrapped) = CombatOrder.NextTurn(CombatOrder.Ordered(combat), req.CombatantId);
            return [new TurnEnded(Actor.Member(member.MemberId), req.CombatantId, next, wrapped ? combat.Round + 1 : combat.Round)];
        }, ct);
    }
}
