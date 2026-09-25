using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Sessions;

public record DeleteSessionNoteRequest
{
    public Guid CampaignId { get; init; }
    public Guid NoteId { get; init; }
}

/// <summary>The author deletes their note. The document goes; the stream keeps every event.</summary>
public class DeleteSessionNote(IDocumentSession session, IHubContext<CampaignHub> hub) : Endpoint<DeleteSessionNoteRequest>
{
    public override void Configure()
    {
        Delete("/api/campaigns/{CampaignId}/notes/{NoteId}");
        Description(b => b.ClearDefaultProduces(StatusCodes.Status200OK).Produces(StatusCodes.Status204NoContent));
    }

    public override async Task HandleAsync(DeleteSessionNoteRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var note = await this.RequireVisibleNote(session, req.CampaignId, req.NoteId, member, ct);
        this.RequireAuthor(note, member);

        session.Events.Append(note.Id, new SessionNoteDeleted(Actor.Member(member.MemberId)));
        await session.SaveChangesAsync(ct);
        await hub.NotifySessionNoteRemoved(note);

        await SendNoContentAsync(ct);
    }
}
