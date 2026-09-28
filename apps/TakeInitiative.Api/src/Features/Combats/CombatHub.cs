using Marten;
using Microsoft.AspNetCore.SignalR;

namespace TakeInitiative.Api.Features.Combats;

/// <summary>
/// Combat pushes (18a.7). One message, <c>combatChanged</c>, carrying the receiver's own
/// <see cref="CombatResponse"/> and its summary, sent after <c>SaveChangesAsync</c>:
/// <list type="bullet">
/// <item>the DM view to <c>campaign:{id}:dm</c>;</item>
/// <item>once the combat has started, each Player member's own view to their
/// <c>member:{id}</c> group, each redacted for exactly that member;</item>
/// <item>a Draft to the DM group only.</item>
/// </list>
/// These are the groups that exist rather than §9's per-combat groups (18's Notes): a role
/// change already moves a connection in or out of the DM group.
/// </summary>
public static class CombatHubContextExtensions
{
    public static async Task NotifyCombatChanged(
        this IHubContext<CampaignHub> hub, IQuerySession session, Campaign campaign, Combat combat, CancellationToken ct)
    {
        var entries = await CombatEntries.Resolve(session, combat, ct);
        var sessionNumber = (await session.LoadAsync<Session>(combat.SessionId, ct))?.Number ?? 0;

        // One payload for every DM, so it is a DM who created none of the entries: an entry a
        // DM made visible to themselves only ("Me") is a plain name in the push, and a link in
        // that DM's own reads.
        var anyDm = new Member { MemberId = Guid.Empty, UserId = Guid.Empty, Role = Role.DM, JoinedAt = default };
        await hub.Clients.Group(CampaignGroups.Dms(campaign.Id))
            .SendAsync(CampaignHubMessages.CombatChanged, Message(combat, anyDm, entries, sessionNumber), ct);

        foreach (var player in campaign.Members.Where(m => m.Role == Role.Player && CombatView.CanSee(combat, m)))
        {
            await hub.Clients.Group(CampaignGroups.Member(player.MemberId))
                .SendAsync(CampaignHubMessages.CombatChanged, Message(combat, player, entries, sessionNumber), ct);
        }
    }

    private static CombatChangedMessage Message(Combat combat, Member viewer, IReadOnlyDictionary<Guid, Entry> entries, int sessionNumber)
        => new(
            CombatView.For(combat, viewer, CombatEntries.VisibleTo(entries, viewer)),
            CombatView.Summary(combat, viewer, sessionNumber));
}
