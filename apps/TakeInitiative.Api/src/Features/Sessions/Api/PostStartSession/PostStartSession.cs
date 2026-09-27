using FastEndpoints;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Sessions;

public record PostStartSessionRequest
{
    public Guid CampaignId { get; init; }
    /// <summary>The number the caller expects to start: the current session's number plus one.</summary>
    public required int Number { get; init; }
}

public class PostStartSessionRequestValidator : Validator<PostStartSessionRequest>
{
    public PostStartSessionRequestValidator()
    {
        RuleFor(x => x.Number).GreaterThan(0);
    }
}

/// <summary>
/// Any member starts the next session. Asking for the current session's number returns
/// it without appending (someone else just started it), so a double tap or two members
/// at once end on the same session. Any other number is a 409.
/// </summary>
public class PostStartSession(IDocumentSession session, IHubContext<CampaignHub> hub) : Endpoint<PostStartSessionRequest, SessionResponse>
{
    public override void Configure()
    {
        Post("/api/campaigns/{CampaignId}/sessions");
    }

    public override async Task HandleAsync(PostStartSessionRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);

        var current = await session.CurrentSession(req.CampaignId, ct);

        if (current is not null && req.Number == current.Number)
        {
            await SendAsync(SessionResponse.From(current, current.Id), cancellation: ct);
            return;
        }
        if (req.Number != (current?.Number ?? 0) + 1)
        {
            // Shown to the member as it is written, so a campaign that has never had a
            // session says so rather than naming a Session 0 that never existed.
            ThrowError(
                current is null
                    ? "No session has been started yet. Start Session 1 first."
                    : $"Session {req.Number} cannot be started: the current session is Session {current.Number}.",
                StatusCodes.Status409Conflict);
        }

        var sessionId = Guid.NewGuid();
        session.Events.StartStream<Session>(sessionId,
            new SessionStarted(Actor.Member(member.MemberId), req.CampaignId, req.Number));
        try
        {
            await session.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            // Someone else started this number between our read and our write. The unique
            // (CampaignId, Number) index rejected ours, so theirs is the current session.
            var winner = await session.CurrentSession(req.CampaignId, ct);
            if (winner is null || winner.Number != req.Number)
            {
                throw;
            }
            await SendAsync(SessionResponse.From(winner, winner.Id), cancellation: ct);
            return;
        }

        // Only the request that appended pushes; a caller who lost the race returns the winner silently.
        var started = SessionResponse.From((await session.LoadAsync<Session>(sessionId, ct))!, sessionId);
        await hub.NotifySessionStarted(req.CampaignId, started);
        await SendAsync(started, cancellation: ct);
    }

    private static bool IsUniqueViolation(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
        {
            if (ex is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return true;
            }
        }
        return false;
    }
}
