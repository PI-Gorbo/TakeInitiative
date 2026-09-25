using FastEndpoints;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Sessions;

public record GetSessionNoteHistoryRequest
{
    public Guid CampaignId { get; init; }
    public Guid NoteId { get; init; }
}

public record GetSessionNoteHistoryResponse
{
    /// <summary>Every version of the note, oldest first. The last one is the current text.</summary>
    public required SessionNoteVersion[] Versions { get; init; }
}

public record SessionNoteVersion
{
    public required string Text { get; init; }
    public required bool IsRecap { get; init; }
    public required DateTimeOffset At { get; init; }
}

/// <summary>A note's edit history, read from its stream. Whoever can see the note can read it.</summary>
public class GetSessionNoteHistory(IDocumentSession session)
    : Endpoint<GetSessionNoteHistoryRequest, GetSessionNoteHistoryResponse>
{
    public override void Configure()
    {
        Get("/api/campaigns/{CampaignId}/notes/{NoteId}/history");
    }

    public override async Task HandleAsync(GetSessionNoteHistoryRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var note = await this.RequireVisibleNote(session, req.CampaignId, req.NoteId, member, ct);

        var events = await session.Events.FetchStreamAsync(note.Id, token: ct);
        var versions = events
            .Select(e => e.Data switch
            {
                SessionNotePosted posted => new SessionNoteVersion { Text = posted.Text, IsRecap = posted.IsRecap, At = e.Timestamp },
                SessionNoteEdited edited => new SessionNoteVersion { Text = edited.Text, IsRecap = edited.IsRecap, At = e.Timestamp },
                _ => null,
            })
            .OfType<SessionNoteVersion>()
            .ToArray();

        await SendAsync(new GetSessionNoteHistoryResponse { Versions = versions }, cancellation: ct);
    }
}
