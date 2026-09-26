using FastEndpoints;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Sessions;

public record PutSessionNoteRequest
{
    public Guid CampaignId { get; init; }
    public Guid NoteId { get; init; }
    public required string Text { get; init; }
    public required bool IsRecap { get; init; }
    /// <summary>Entries to create with the edit, each already mentioned in <see cref="Text"/> (see <see cref="NewEntries"/>).</summary>
    public NewEntryRequest[]? NewEntries { get; init; }
}

public class PutSessionNoteRequestValidator : Validator<PutSessionNoteRequest>
{
    public PutSessionNoteRequestValidator()
    {
        RuleFor(x => x.Text).SessionNoteText();
        RuleFor(x => x.NewEntries).NewEntriesList();
    }
}

/// <summary>
/// The author edits their note. An unchanged note appends nothing. Last write wins: only
/// the author can edit, so a conflict is the same person on two devices. <c>newEntries</c>
/// are created in the same transaction, with the note's current visibility.
/// </summary>
public class PutSessionNote(IDocumentSession session, IHubContext<CampaignHub> hub) : Endpoint<PutSessionNoteRequest, SessionNoteResponse>
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
        var newEntryIds = await this.AppendNewEntries(
            session, req.CampaignId, member, note.Id, text, NewEntries.VisibilityFrom(note), req.NewEntries, ct);
        var edited = note.Text != text || note.IsRecap != req.IsRecap;
        if (edited)
        {
            session.Events.Append(note.Id, new SessionNoteEdited(Actor.Member(member.MemberId), text, req.IsRecap));
        }
        if (edited || newEntryIds.Count > 0)
        {
            await session.SaveChangesAsync(ct);
        }
        if (edited)
        {
            note = (await session.LoadAsync<SessionNote>(note.Id, ct))!;
            await hub.NotifySessionNoteUpserted(note);
        }
        await hub.NotifyCreated(session, newEntryIds, ct);

        await SendAsync(SessionNoteResponse.From(note), cancellation: ct);
    }
}
