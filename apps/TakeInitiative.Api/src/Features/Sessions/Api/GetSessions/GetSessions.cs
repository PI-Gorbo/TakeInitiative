using FastEndpoints;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Sessions;

public record GetSessionsRequest
{
    public Guid CampaignId { get; init; }
}

public record GetSessionsResponse
{
    /// <summary>Every session of the campaign, newest first.</summary>
    public required SessionResponse[] Sessions { get; init; }
    public required Guid CurrentSessionId { get; init; }
    /// <summary>Whether the composer should offer to start the next session (the gap prompt).</summary>
    public required bool SuggestNextSession { get; init; }
}

/// <summary>Every session of the campaign, newest first, with the current session and the gap prompt. Members only.</summary>
public class GetSessions(IDocumentSession session, [FromKeyedServices(SessionGap.ClockKey)] TimeProvider clock) : Endpoint<GetSessionsRequest, GetSessionsResponse>
{
    public override void Configure()
    {
        Get("/api/campaigns/{CampaignId}/sessions");
    }

    public override async Task HandleAsync(GetSessionsRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);

        var sessions = await session.Query<Session>()
            .Where(s => s.CampaignId == req.CampaignId)
            .OrderByDescending(s => s.Number)
            .ToListAsync(ct);
        if (sessions.Count == 0)
        {
            ThrowError("This campaign has no sessions.", StatusCodes.Status404NotFound);
        }

        var current = sessions[0];
        await SendAsync(new GetSessionsResponse
        {
            Sessions = sessions.Select(s => SessionResponse.From(s, current.Id)).ToArray(),
            CurrentSessionId = current.Id,
            SuggestNextSession = await SessionGap.SuggestNextSession(session, current, member, clock, ct),
        }, cancellation: ct);
    }
}
