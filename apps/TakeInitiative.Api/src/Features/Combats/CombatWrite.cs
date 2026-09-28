using FastEndpoints;
using Marten;
using Marten.Events;
using Marten.Exceptions;
using Microsoft.AspNetCore.SignalR;

namespace TakeInitiative.Api.Features.Combats;

/// <summary>
/// The shape of every combat write: read the stream at its version
/// (<c>FetchForWriting</c>), check, append at that version, save, then push each receiver's
/// view and answer with the caller's (18a.6, 18a.7). Two writes at once fail the later one
/// with a 409 rather than applying it over a state it did not check.
/// </summary>
public static class CombatWrite
{
    public const string ConflictMessage = "The combat changed while you were saving. Try again.";

    /// <summary>Loads the combat for writing: visible to the caller (404) and not finished (409).</summary>
    public static async Task<(IEventStream<Combat> Stream, Combat Combat)> Open<TRequest, TResponse>(
        this Endpoint<TRequest, TResponse> endpoint, IDocumentSession session, Guid campaignId, Guid combatId, Member caller, CancellationToken ct)
        where TRequest : notnull
    {
        var stream = await session.Events.FetchForWriting<Combat>(combatId, ct);
        var combat = endpoint.RequireVisible(stream.Aggregate, campaignId, caller);
        endpoint.RequireNotFinished(combat);
        return (stream, combat);
    }

    /// <summary>Saves what was appended, pushes the result and answers with the caller's view.</summary>
    public static async Task Commit<TRequest>(
        this Endpoint<TRequest, CombatResponse> endpoint, IDocumentSession session, IHubContext<CampaignHub> hub,
        Campaign campaign, Guid combatId, Member caller, CancellationToken ct)
        where TRequest : notnull
    {
        try
        {
            await session.SaveChangesAsync(ct);
        }
        catch (ConcurrencyException)
        {
            endpoint.ThrowError(ConflictMessage, StatusCodes.Status409Conflict);
        }
        var combat = (await session.LoadAsync<Combat>(combatId, ct))!;
        await hub.NotifyCombatChanged(session, campaign, combat, ct);
        await endpoint.HttpContext.Response.SendAsync(await CombatEntries.ViewFor(session, combat, caller, ct), cancellation: ct);
    }
}
