using System.Net;
using FastEndpoints;
using Marten;

namespace TakeInitiative.Api.Features.Sessions;

/// <summary>Loads for the session endpoints, after <see cref="CampaignAccess.RequireMember"/>.</summary>
public static class SessionAccess
{
    /// <summary>The campaign's current session. Only a campaign from before step 14 has none.</summary>
    public static async Task<Session> RequireCurrentSession<TRequest, TResponse>(
        this Endpoint<TRequest, TResponse> endpoint, IQuerySession session, Guid campaignId, CancellationToken ct)
        where TRequest : notnull
    {
        var current = await session.CurrentSession(campaignId, ct);
        if (current is null)
        {
            endpoint.ThrowError("This campaign has no sessions.", (int)HttpStatusCode.NotFound);
        }
        return current;
    }

    /// <summary>A session of this campaign. A session of another campaign is a 404.</summary>
    public static async Task<Session> RequireSession<TRequest, TResponse>(
        this Endpoint<TRequest, TResponse> endpoint, IQuerySession session, Guid campaignId, Guid sessionId, CancellationToken ct)
        where TRequest : notnull
    {
        var found = await session.LoadAsync<Session>(sessionId, ct);
        if (found is null || found.CampaignId != campaignId)
        {
            endpoint.ThrowError("There is no session with the given id in this campaign.", (int)HttpStatusCode.NotFound);
        }
        return found;
    }

    /// <summary>
    /// A note of this campaign that the viewer can see. Anything else is a 404, never a
    /// 403, so a note's existence does not leak (invariant 5).
    /// </summary>
    public static async Task<SessionNote> RequireVisibleNote<TRequest, TResponse>(
        this Endpoint<TRequest, TResponse> endpoint, IQuerySession session, Guid campaignId, Guid noteId, Member viewer, CancellationToken ct)
        where TRequest : notnull
    {
        var note = await session.LoadAsync<SessionNote>(noteId, ct);
        if (note is null || note.CampaignId != campaignId || !SessionNoteVisibility.CanSee(note, viewer))
        {
            endpoint.ThrowError("There is no session note with the given id.", (int)HttpStatusCode.NotFound);
        }
        return note;
    }

    /// <summary>Only a note's author edits it, changes its visibility or deletes it (invariant 4).</summary>
    public static void RequireAuthor<TRequest, TResponse>(this Endpoint<TRequest, TResponse> endpoint, SessionNote note, Member caller)
        where TRequest : notnull
    {
        if (note.AuthorMemberId != caller.MemberId)
        {
            endpoint.ThrowError("Only the author of a session note can change it.", (int)HttpStatusCode.Forbidden);
        }
    }

    public static void RequireDm<TRequest, TResponse>(this Endpoint<TRequest, TResponse> endpoint, Member caller, string message)
        where TRequest : notnull
    {
        if (caller.Role != Role.DM)
        {
            endpoint.ThrowError(message, (int)HttpStatusCode.Forbidden);
        }
    }
}
