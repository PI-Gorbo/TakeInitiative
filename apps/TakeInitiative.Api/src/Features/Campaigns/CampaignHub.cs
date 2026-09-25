using System.Collections.Concurrent;
using CSharpFunctionalExtensions;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Campaigns;

/// <summary>SignalR group names (design §9). Each change is sent only to the groups allowed to see it.</summary>
public static class CampaignGroups
{
    public static string Campaign(Guid campaignId) => $"campaign:{campaignId}";
    public static string Dms(Guid campaignId) => $"campaign:{campaignId}:dm";
    public static string Member(Guid memberId) => $"member:{memberId}";

    /// <summary>
    /// The groups <see cref="CampaignHub.Join"/> puts a member's connection in. The push-side
    /// visibility rule (<c>SessionNoteAudience</c>) is tested against this.
    /// </summary>
    public static IReadOnlyList<string> Of(Guid campaignId, Member member) => member.Role == Role.DM
        ? [Campaign(campaignId), Member(member.MemberId), Dms(campaignId)]
        : [Campaign(campaignId), Member(member.MemberId)];
}

/// <summary>Client method names the hub sends.</summary>
public static class CampaignHubMessages
{
    public const string MemberJoined = "memberJoined";
    public const string MemberRoleChanged = "memberRoleChanged";

    // Sessions and notes (step 14b). Note messages go only to the note's audience.
    public const string SessionStarted = "sessionStarted";
    public const string SessionTitleChanged = "sessionTitleChanged";
    public const string SessionNoteUpserted = "sessionNoteUpserted";
    public const string SessionNoteRemoved = "sessionNoteRemoved";
    public const string SessionNoteHidden = "sessionNoteHidden";
}

/// <summary>
/// Which hub connections belong to which member, so a role change can move that
/// member's live connections in or out of the DM group without trusting the client
/// to reconnect. In memory: the API runs as a single instance.
/// </summary>
public class CampaignConnections
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _byMember = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, byte>> _byConnection = new();

    public void Track(string connectionId, Guid memberId)
    {
        _byMember.GetOrAdd(memberId, _ => new()).TryAdd(connectionId, 0);
        _byConnection.GetOrAdd(connectionId, _ => new()).TryAdd(memberId, 0);
    }

    public void Untrack(string connectionId, Guid memberId)
    {
        if (_byMember.TryGetValue(memberId, out var connections))
        {
            connections.TryRemove(connectionId, out _);
        }
        if (_byConnection.TryGetValue(connectionId, out var members))
        {
            members.TryRemove(memberId, out _);
        }
    }

    public void UntrackConnection(string connectionId)
    {
        if (_byConnection.TryRemove(connectionId, out var members))
        {
            foreach (var memberId in members.Keys)
            {
                if (_byMember.TryGetValue(memberId, out var connections))
                {
                    connections.TryRemove(connectionId, out _);
                }
            }
        }
    }

    public IReadOnlyList<string> ConnectionsOf(Guid memberId)
        => _byMember.TryGetValue(memberId, out var connections) ? connections.Keys.ToList() : [];
}

public static class CampaignHubContextExtensions
{
    public static Task NotifyMemberJoined(this IHubContext<CampaignHub> hub, Guid campaignId, Guid memberId)
        => hub.Clients.Group(CampaignGroups.Campaign(campaignId))
            .SendAsync(CampaignHubMessages.MemberJoined, new { campaignId, memberId });

    /// <summary>
    /// Moves the member's connections in or out of the DM group, then tells the campaign.
    /// </summary>
    public static async Task NotifyMemberRoleChanged(
        this IHubContext<CampaignHub> hub, CampaignConnections connections, Guid campaignId, Guid memberId, Role role)
    {
        var dmGroup = CampaignGroups.Dms(campaignId);
        foreach (var connectionId in connections.ConnectionsOf(memberId))
        {
            if (role == Role.DM)
            {
                await hub.Groups.AddToGroupAsync(connectionId, dmGroup);
            }
            else
            {
                await hub.Groups.RemoveFromGroupAsync(connectionId, dmGroup);
            }
        }

        await hub.Clients.Group(CampaignGroups.Campaign(campaignId))
            .SendAsync(CampaignHubMessages.MemberRoleChanged, new { campaignId, memberId, role });
    }
}

[Authorize] // TakePolicies.UserExists
public class CampaignHub(CampaignConnections connections) : Hub
{
    /// <summary>
    /// Adds the caller to <c>campaign:{id}</c>, <c>member:{memberId}</c>, and <c>campaign:{id}:dm</c>
    /// when they are a DM. Membership is checked against the Campaign projection.
    /// </summary>
    public async Task Join(IDocumentSession session, Guid CampaignId)
    {
        // The caller's id comes from the authenticated connection, never from the client.
        Result<Guid> callerUserId = Context.User.GetUserId();
        if (callerUserId.IsFailure)
        {
            throw new OperationCanceledException(callerUserId.Error);
        }

        var campaign = await session.LoadAsync<Campaign>(CampaignId);
        var member = campaign?.MemberForUser(callerUserId.Value);
        if (campaign is null || member is null)
        {
            throw new OperationCanceledException("The user must be part of the campaign to join the hub.");
        }

        foreach (var group in CampaignGroups.Of(CampaignId, member))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, group);
        }
        if (member.Role != Role.DM)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, CampaignGroups.Dms(CampaignId));
        }
        connections.Track(Context.ConnectionId, member.MemberId);
    }

    public async Task Leave(IDocumentSession session, Guid CampaignId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, CampaignGroups.Campaign(CampaignId));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, CampaignGroups.Dms(CampaignId));

        var userId = Context.User.GetUserId();
        var campaign = userId.IsSuccess ? await session.LoadAsync<Campaign>(CampaignId) : null;
        var member = campaign?.MemberForUser(userId.Value);
        if (member is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, CampaignGroups.Member(member.MemberId));
            connections.Untrack(Context.ConnectionId, member.MemberId);
        }
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.UntrackConnection(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
