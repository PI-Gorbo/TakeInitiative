using Microsoft.AspNetCore.SignalR;

namespace TakeInitiative.Api.Features.Sessions;

/// <summary>
/// The push-side twin of <see cref="SessionNoteVisibility"/> (invariant 5): the hub groups
/// whose members may see a note. A viewer is in <c>campaign:{id}</c>, <c>member:{theirId}</c>
/// and, when a DM, <c>campaign:{id}:dm</c> (<see cref="CampaignGroups.Of"/>), so a viewer can
/// see a note exactly when they are in one of <see cref="Groups"/>. The unit tests check that
/// for every case; change the two together.
/// </summary>
public static class SessionNoteAudience
{
    /// <summary>
    /// The note's <see cref="Audience"/>, owned by its author. Hiding is the notes' own case:
    /// a hidden <c>Everyone</c> note narrows to the DMs and its author.
    /// </summary>
    public static Audience Of(SessionNote note) => note is { Visibility: Visibility.Everyone, IsHidden: true }
        ? Audience.Of(Visibility.DM, note.AuthorMemberId)
        : Audience.Of(note.Visibility, note.AuthorMemberId);

    public static IReadOnlyList<string> Groups(SessionNote note, Guid campaignId) => Of(note).Groups(campaignId);
}

/// <summary><c>sessionNoteRemoved</c>: drop this note. Sent only to groups that could see it.</summary>
public record SessionNoteRemovedMessage(Guid NoteId, Guid SessionId);

/// <summary><c>sessionNoteHidden</c>: sent to the author alone, so they are told a DM hid their note.</summary>
public record SessionNoteHiddenMessage(Guid NoteId, Guid SessionId, Guid ByMemberId);

/// <summary>
/// Session and note pushes. Every endpoint calls these after <c>SaveChangesAsync</c>, with
/// the note or session as it is after the change. Payloads carry no per-viewer fields, so
/// one payload goes to every allowed group.
/// </summary>
public static class SessionHubContextExtensions
{
    public static Task NotifySessionStarted(this IHubContext<CampaignHub> hub, Guid campaignId, SessionResponse session)
        => hub.Clients.Group(CampaignGroups.Campaign(campaignId)).SendAsync(CampaignHubMessages.SessionStarted, session);

    public static Task NotifySessionTitleChanged(this IHubContext<CampaignHub> hub, Guid campaignId, SessionResponse session)
        => hub.Clients.Group(CampaignGroups.Campaign(campaignId)).SendAsync(CampaignHubMessages.SessionTitleChanged, session);

    /// <summary>Post, edit and unhide: the note to its audience.</summary>
    public static Task NotifySessionNoteUpserted(this IHubContext<CampaignHub> hub, SessionNote note)
        => hub.Clients.Groups(SessionNoteAudience.Groups(note, note.CampaignId))
            .SendAsync(CampaignHubMessages.SessionNoteUpserted, SessionNoteResponse.From(note));

    /// <summary>Delete: the removal to everyone who could see the note.</summary>
    public static Task NotifySessionNoteRemoved(this IHubContext<CampaignHub> hub, SessionNote before)
        => hub.Clients.Groups(SessionNoteAudience.Groups(before, before.CampaignId))
            .SendAsync(CampaignHubMessages.SessionNoteRemoved, new SessionNoteRemovedMessage(before.Id, before.SessionId));

    /// <summary>
    /// Visibility change and hide: a removal to the groups that lose the note, then the note
    /// to its new audience. Messages on one connection arrive in order, so a connection in
    /// both ends with the note, and the removal goes only to groups that could already see
    /// it, so its id leaks nothing.
    /// </summary>
    public static async Task NotifySessionNoteMoved(this IHubContext<CampaignHub> hub, SessionNote before, SessionNote after)
    {
        var afterGroups = SessionNoteAudience.Groups(after, after.CampaignId);
        // Every connection is in the campaign group, so a note the whole campaign can now see
        // loses nobody. Otherwise the DM and member groups that are no longer targeted lose it.
        List<string> losing = afterGroups.Contains(CampaignGroups.Campaign(after.CampaignId))
            ? []
            : SessionNoteAudience.Groups(before, before.CampaignId).Except(afterGroups).ToList();
        if (losing.Count > 0)
        {
            await hub.Clients.Groups(losing)
                .SendAsync(CampaignHubMessages.SessionNoteRemoved, new SessionNoteRemovedMessage(before.Id, before.SessionId));
        }
        await hub.Clients.Groups(afterGroups)
            .SendAsync(CampaignHubMessages.SessionNoteUpserted, SessionNoteResponse.From(after));
    }

    /// <summary>Hide: tells the author. Not sent when a DM hides their own note.</summary>
    public static Task NotifySessionNoteHidden(this IHubContext<CampaignHub> hub, SessionNote note, Guid byMemberId)
        => note.AuthorMemberId == byMemberId
            ? Task.CompletedTask
            : hub.Clients.Group(CampaignGroups.Member(note.AuthorMemberId))
                .SendAsync(CampaignHubMessages.SessionNoteHidden, new SessionNoteHiddenMessage(note.Id, note.SessionId, byMemberId));
}
