using System.Net;
using FastEndpoints;
using Marten;

namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// Loads and checks for the entry endpoints, after <see cref="CampaignAccess.RequireMember"/>.
/// The order is the 14a one: load, read rule (404), then permission (403).
/// </summary>
public static class EntryAccess
{
    /// <summary>
    /// An entry of this campaign that the viewer can see. Anything else is a 404, never a
    /// 403, so an entry's existence does not leak (invariant 5).
    /// </summary>
    public static async Task<Entry> RequireVisibleEntry<TRequest, TResponse>(
        this Endpoint<TRequest, TResponse> endpoint, IQuerySession session, Guid campaignId, Guid entryId, Member viewer, CancellationToken ct)
        where TRequest : notnull
    {
        var entry = await session.LoadAsync<Entry>(entryId, ct);
        if (entry is null || entry.CampaignId != campaignId || !EntryVisibility.CanSee(entry, viewer))
        {
            endpoint.ThrowError("There is no entry with the given id.", (int)HttpStatusCode.NotFound);
        }
        return entry;
    }

    /// <summary>Name, kind and aliases: see <see cref="EntryPermissions.CanEdit"/>.</summary>
    public static void RequireCanEdit<TRequest, TResponse>(this Endpoint<TRequest, TResponse> endpoint, Entry entry, Member caller)
        where TRequest : notnull
    {
        if (!EntryPermissions.CanEdit(entry, caller))
        {
            endpoint.ThrowError("Only its creator and the DMs can edit this entry.", (int)HttpStatusCode.Forbidden);
        }
    }

    /// <summary>Visibility and edit access: see <see cref="EntryPermissions.CanChangeAccess"/>.</summary>
    public static void RequireCreatorOrDm<TRequest, TResponse>(this Endpoint<TRequest, TResponse> endpoint, Entry entry, Member caller)
        where TRequest : notnull
    {
        if (!EntryPermissions.CanChangeAccess(entry, caller))
        {
            endpoint.ThrowError("Only the entry's creator and the DMs can change who sees or edits it.", (int)HttpStatusCode.Forbidden);
        }
    }
}
