using FastEndpoints;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Sessions;

public record GetSessionNoteRequest
{
    public Guid CampaignId { get; init; }
    public Guid NoteId { get; init; }
}

public record GetSessionNoteResponse
{
    public required SessionNoteResponse Note { get; init; }
    /// <summary>The number of the note's session, so a deep link knows how far back to page.</summary>
    public required int SessionNumber { get; init; }
}

/// <summary>One session note, for deep links. A note the caller cannot see is a 404.</summary>
public class GetSessionNote(IDocumentSession session) : Endpoint<GetSessionNoteRequest, GetSessionNoteResponse>
{
    public override void Configure()
    {
        Get("/api/campaigns/{CampaignId}/notes/{NoteId}");
    }

    public override async Task HandleAsync(GetSessionNoteRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var note = await this.RequireVisibleNote(session, req.CampaignId, req.NoteId, member, ct);
        var noteSession = await this.RequireSession(session, req.CampaignId, note.SessionId, ct);

        await SendAsync(new GetSessionNoteResponse
        {
            Note = SessionNoteResponse.From(note),
            SessionNumber = noteSession.Number,
        }, cancellation: ct);
    }
}
