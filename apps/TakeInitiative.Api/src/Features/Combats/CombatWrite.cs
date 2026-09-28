using FastEndpoints;
using Marten;
using Marten.Events;
using Marten.Exceptions;
using Microsoft.AspNetCore.SignalR;

namespace TakeInitiative.Api.Features.Combats;

/// <summary>
/// The shape of every combat write: read the stream at its version (<c>FetchForWriting</c>),
/// decide what to append from that state, append at that version, save, then push each
/// receiver's view and answer with the caller's (18a.6, 18a.7).
/// <para>
/// Two writes at once fail the later one with a concurrency error. It is then decided again
/// on the fresh state, up to <see cref="Attempts"/> times (18b.7), so every check runs against
/// the state the events land on: a stale end turn becomes its 409, a roll with nothing left
/// waiting appends nothing, and an edit applies over the other write.
/// </para>
/// </summary>
public static class CombatWrite
{
    public const string ConflictMessage = "The combat changed while you were saving. Try again.";

    /// <summary>How many times a write is decided before a concurrency error is the caller's 409.</summary>
    public const int Attempts = 3;

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

    /// <summary>
    /// Opens the combat, asks <paramref name="decide"/> for the events to append (it may throw
    /// the endpoint's errors), saves them, retrying on a concurrent write, then pushes and
    /// answers with the caller's view. No events is a 200 that appends nothing.
    /// </summary>
    public static async Task Write<TRequest>(
        this Endpoint<TRequest, CombatResponse> endpoint, IDocumentSession session, IHubContext<CampaignHub> hub,
        Campaign campaign, Guid combatId, Member caller, Func<Combat, Task<IReadOnlyList<object>>> decide, CancellationToken ct)
        where TRequest : notnull
    {
        for (var attempt = 1; ; attempt++)
        {
            var (stream, combat) = await endpoint.Open(session, campaign.Id, combatId, caller, ct);
            var events = await decide(combat);
            if (events.Count == 0)
            {
                break;
            }
            stream.AppendMany(events);
            try
            {
                await session.SaveChangesAsync(ct);
                break;
            }
            catch (Exception e) when (IsConcurrency(e))
            {
                if (attempt >= Attempts)
                {
                    endpoint.ThrowError(ConflictMessage, StatusCodes.Status409Conflict);
                }
                session.EjectAllPendingChanges();
            }
        }

        var saved = (await session.LoadAsync<Combat>(combatId, ct))!;
        await hub.NotifyCombatChanged(session, campaign, saved, ct);
        await endpoint.HttpContext.Response.SendAsync(await CombatEntries.ViewFor(session, saved, caller, ct), cancellation: ct);
    }

    /// <inheritdoc cref="Write{TRequest}(Endpoint{TRequest, CombatResponse}, IDocumentSession, IHubContext{CampaignHub}, Campaign, Guid, Member, Func{Combat, Task{IReadOnlyList{object}}}, CancellationToken)"/>
    public static Task Write<TRequest>(
        this Endpoint<TRequest, CombatResponse> endpoint, IDocumentSession session, IHubContext<CampaignHub> hub,
        Campaign campaign, Guid combatId, Member caller, Func<Combat, IReadOnlyList<object>> decide, CancellationToken ct)
        where TRequest : notnull
        => endpoint.Write(session, hub, campaign, combatId, caller, combat => Task.FromResult(decide(combat)), ct);

    private static bool IsConcurrency(Exception e)
        => e is ConcurrencyException or EventStreamUnexpectedMaxEventIdException;
}
