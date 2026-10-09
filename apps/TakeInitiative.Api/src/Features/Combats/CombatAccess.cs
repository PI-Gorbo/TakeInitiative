using System.Net;
using FastEndpoints;
using Marten;

namespace TakeInitiative.Api.Features.Combats;

/// <summary>
/// Who can do what with a combat (18a.4, design §8), after <see cref="CampaignAccess.RequireMember"/>.
/// <list type="table">
/// <item>Create, finish, roll everyone, reorder, history: DMs (403 otherwise).</item>
/// <item>See a combat: DMs any; players once it has started. Anything else is a 404, never a
/// 403, so a Draft's existence does not leak.</item>
/// <item>Add combatants: DMs, in any status but Finished; a player one combatant a call, from a
/// Character entry they have claimed, not one already in the combat.</item>
/// <item>Edit a combatant: DMs every field; a player their own HP, max HP, conditions, and
/// initiative while it waits.</item>
/// <item>Remove a combatant: DMs any; a player their own.</item>
/// </list>
/// A Finished combat is read-only: every write is a 409.
/// </summary>
public static class CombatAccess
{
    public const string NotFoundMessage = "There is no combat with the given id.";
    public const string FinishedMessage = "This combat has finished.";

    /// <summary>A combat of this campaign that the viewer can see, or a 404.</summary>
    public static async Task<Combat> RequireVisibleCombat<TRequest, TResponse>(
        this Endpoint<TRequest, TResponse> endpoint, IQuerySession session, Guid campaignId, Guid combatId, Member viewer, CancellationToken ct)
        where TRequest : notnull
    {
        var combat = await session.LoadAsync<Combat>(combatId, ct);
        return RequireVisible(endpoint, combat, campaignId, viewer);
    }

    /// <summary>The same check on a combat already loaded (for a write, at the version it appends at).</summary>
    public static Combat RequireVisible<TRequest, TResponse>(
        this Endpoint<TRequest, TResponse> endpoint, Combat? combat, Guid campaignId, Member viewer)
        where TRequest : notnull
    {
        if (combat is null || combat.CampaignId != campaignId || !CombatView.CanSee(combat, viewer))
        {
            endpoint.ThrowError(NotFoundMessage, (int)HttpStatusCode.NotFound);
        }
        return combat;
    }

    public static void RequireNotFinished<TRequest, TResponse>(this Endpoint<TRequest, TResponse> endpoint, Combat combat)
        where TRequest : notnull
    {
        if (combat.Status == CombatStatus.Finished)
        {
            endpoint.ThrowError(FinishedMessage, (int)HttpStatusCode.Conflict);
        }
    }

    /// <summary>
    /// A combatant the viewer can see (a 404 otherwise, so a hidden one does not leak), which
    /// they may change: DMs any, a player their own (403).
    /// </summary>
    public static Combatant RequireChangeableCombatant<TRequest, TResponse>(
        this Endpoint<TRequest, TResponse> endpoint, Combat combat, Guid combatantId, Member caller)
        where TRequest : notnull
    {
        var combatant = combat.Find(combatantId);
        if (combatant is null || !CombatView.CanSee(combatant, caller))
        {
            endpoint.ThrowError("There is no combatant with the given id.", (int)HttpStatusCode.NotFound);
        }
        if (!CanChange(combatant, caller))
        {
            endpoint.ThrowError("Players can only change their own combatant.", (int)HttpStatusCode.Forbidden);
        }
        return combatant;
    }

    public static bool CanChange(Combatant combatant, Member caller)
        => caller.Role == Role.DM || (combatant.OwnerMemberId is { } owner && owner == caller.MemberId);

    /// <summary>
    /// The fields of <paramref name="after"/> a player may not change on their own combatant,
    /// by name; empty when the edit is allowed. DMs may change everything.
    /// </summary>
    public static IReadOnlyList<string> ForbiddenChanges(CombatantState before, CombatantState after, Member caller)
    {
        if (caller.Role == Role.DM)
        {
            return [];
        }
        var forbidden = new List<string>();
        if (after.Name != before.Name) forbidden.Add("name");
        if (after.Ac != before.Ac) forbidden.Add("ac");
        if (after.Hidden != before.Hidden) forbidden.Add("hidden");
        if (after.PlayersSee != before.PlayersSee) forbidden.Add("playersSee");
        // Only a waiting combatant's initiative is theirs to type; a placed one stays put.
        if (after.Initiative != before.Initiative && before.Initiative is not null) forbidden.Add("initiative");
        return forbidden;
    }
}
