using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Sessions;

public record PutSessionNoteHiddenRequest
{
    public Guid CampaignId { get; init; }
    public Guid NoteId { get; init; }
    public required bool Hidden { get; init; }
}

/// <summary>
/// A DM hides or unhides a note. A hidden note is seen only by its author and the DMs.
/// Hiding a hidden note (or unhiding a visible one) appends nothing.
/// </summary>
public class PutSessionNoteHidden(IDocumentSession session, IHubContext<CampaignHub> hub) : Endpoint<PutSessionNoteHiddenRequest, SessionNoteResponse>
{
    public override void Configure()
    {
        Put("/api/campaigns/{CampaignId}/notes/{NoteId}/hidden");
    }

    public override async Task HandleAsync(PutSessionNoteHiddenRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var note = await this.RequireVisibleNote(session, req.CampaignId, req.NoteId, member, ct);
        this.RequireDm(member, "Only a DM can hide or unhide a session note.");

        if (note.IsHidden != req.Hidden)
        {
            var actor = Actor.Member(member.MemberId);
            session.Events.Append(note.Id, req.Hidden ? new SessionNoteHidden(actor) : new SessionNoteUnhidden(actor));
            await session.SaveChangesAsync(ct);
            var before = note;
            note = (await session.LoadAsync<SessionNote>(note.Id, ct))!;
            if (req.Hidden)
            {
                // Players lose the note (removed); DMs and the author keep it, now marked.
                await hub.NotifySessionNoteMoved(before, note);
                await hub.NotifySessionNoteHidden(note, member.MemberId);
            }
            else
            {
                await hub.NotifySessionNoteUpserted(note);
            }
        }

        await SendAsync(SessionNoteResponse.From(note), cancellation: ct);
    }
}
