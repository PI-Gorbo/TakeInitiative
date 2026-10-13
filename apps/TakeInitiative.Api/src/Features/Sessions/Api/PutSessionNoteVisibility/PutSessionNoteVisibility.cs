using FastEndpoints;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Sessions;

public record PutSessionNoteVisibilityRequest
{
    public Guid CampaignId { get; init; }
    public Guid NoteId { get; init; }
    public required Visibility Visibility { get; init; }
}

public class PutSessionNoteVisibilityRequestValidator : Validator<PutSessionNoteVisibilityRequest>
{
    public PutSessionNoteVisibilityRequestValidator()
    {
        RuleFor(x => x.Visibility).IsInEnum();
    }
}

/// <summary>The author changes who can see their note. The same visibility appends nothing.</summary>
public class PutSessionNoteVisibility(IDocumentSession session, IHubContext<CampaignHub> hub)
    : Endpoint<PutSessionNoteVisibilityRequest, SessionNoteResponse>
{
    public override void Configure()
    {
        Put("/api/campaigns/{CampaignId}/notes/{NoteId}/visibility");
    }

    public override async Task HandleAsync(PutSessionNoteVisibilityRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var note = await this.RequireVisibleNote(session, req.CampaignId, req.NoteId, member, ct);
        this.RequireAuthor(note, member);

        if (note.Visibility != req.Visibility)
        {
            var actor = Actor.Member(member.MemberId);
            session.Events.Append(note.Id, new SessionNoteVisibilityChanged(actor, req.Visibility));
            // Leaving Everyone takes its images out of public view, so no entry may keep one as
            // its primary image (SAM-12). Coming back to Everyone does not restore it.
            IReadOnlyList<Guid> cleared = req.Visibility == Visibility.Everyone
                ? []
                : await EntryPrimaryImages.ClearStaleFor(session, note, actor, ct);
            await session.SaveChangesAsync(ct);
            var before = note;
            note = (await session.LoadAsync<SessionNote>(note.Id, ct))!;
            await EntryPrimaryImages.NotifyCleared(hub, session, cleared, ct);
            await hub.NotifySessionNoteMoved(before, note);
        }

        await SendAsync(SessionNoteResponse.From(note), cancellation: ct);
    }
}
