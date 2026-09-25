using Microsoft.AspNetCore.SignalR;

namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// The push-side twin of <see cref="EntryVisibility"/> (invariant 5): the hub groups whose
/// members may see an entry. Both are <see cref="Audience"/> owned by the creator, so a
/// viewer can see an entry exactly when they are in one of <see cref="Groups"/>.
/// </summary>
public static class EntryAudience
{
    public static Audience Of(Entry entry) => Audience.Of(entry.Visibility, entry.CreatorMemberId);

    public static IReadOnlyList<string> Groups(Entry entry) => Of(entry).Groups(entry.CampaignId);
}

/// <summary><c>entryRemoved</c>: drop this entry. Sent only to groups that could see it.</summary>
public record EntryRemovedMessage(Guid EntryId);

/// <summary>
/// Entry pushes. Every endpoint calls these after <c>SaveChangesAsync</c>, with the entry as
/// it is after the change, and only when something was appended. Payloads carry no
/// per-viewer fields, so one payload goes to every allowed group.
/// </summary>
public static class EntryHubContextExtensions
{
    /// <summary>Create, rename, kind, aliases and edit access: the entry to its audience.</summary>
    public static Task NotifyEntryUpserted(this IHubContext<CampaignHub> hub, Entry entry)
        => hub.Clients.Groups(EntryAudience.Groups(entry))
            .SendAsync(CampaignHubMessages.EntryUpserted, EntrySummaryResponse.From(entry));

    /// <summary>
    /// Visibility change: a removal to the groups that lose the entry, then the entry to its
    /// new audience (14b's <c>NotifySessionNoteMoved</c> rule). Messages on one connection
    /// arrive in order, so a connection in both ends with the entry, and the removal goes
    /// only to groups that could already see it, so its id leaks nothing.
    /// </summary>
    public static async Task NotifyEntryMoved(this IHubContext<CampaignHub> hub, Entry before, Entry after)
    {
        var afterGroups = EntryAudience.Groups(after);
        // Every connection is in the campaign group, so an entry the whole campaign can now
        // see loses nobody. Otherwise the DM and member groups no longer targeted lose it.
        List<string> losing = afterGroups.Contains(CampaignGroups.Campaign(after.CampaignId))
            ? []
            : EntryAudience.Groups(before).Except(afterGroups).ToList();
        if (losing.Count > 0)
        {
            await hub.Clients.Groups(losing).SendAsync(CampaignHubMessages.EntryRemoved, new EntryRemovedMessage(before.Id));
        }
        await hub.Clients.Groups(afterGroups)
            .SendAsync(CampaignHubMessages.EntryUpserted, EntrySummaryResponse.From(after));
    }
}
