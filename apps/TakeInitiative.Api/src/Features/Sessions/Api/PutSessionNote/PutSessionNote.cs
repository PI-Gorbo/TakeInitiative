using FastEndpoints;
using FluentValidation;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Sessions;

public record PutSessionNoteRequest
{
    public Guid CampaignId { get; init; }
    public Guid NoteId { get; init; }
    public required string Text { get; init; }
    public required bool IsRecap { get; init; }
}

public class PutSessionNoteRequestValidator : Validator<PutSessionNoteRequest>
{
    public PutSessionNoteRequestValidator()
    {
        RuleFor(x => x.Text).SessionNoteText();
    }
}

/// <summary>
/// The author edits their note. An unchanged note appends nothing. Last write wins: only
/// the author can edit, so a conflict is the same person on two devices.
/// </summary>
public class PutSessionNote(IDocumentSession session) : Endpoint<PutSessionNoteRequest, SessionNoteResponse>
{
    public override void Configure()
    {
        Put("/api/campaigns/{CampaignId}/notes/{NoteId}");
    }

    public override async Task HandleAsync(PutSessionNoteRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var note = await this.RequireVisibleNote(session, req.CampaignId, req.NoteId, member, ct);
        this.RequireAuthor(note, member);

        var text = req.Text.Trim();
        if (note.Text != text || note.IsRecap != req.IsRecap)
        {
            session.Events.Append(note.Id, new SessionNoteEdited(Actor.Member(member.MemberId), text, req.IsRecap));
            await session.SaveChangesAsync(ct);
            note = (await session.LoadAsync<SessionNote>(note.Id, ct))!;
        }

        await SendAsync(SessionNoteResponse.From(note), cancellation: ct);
    }
}
