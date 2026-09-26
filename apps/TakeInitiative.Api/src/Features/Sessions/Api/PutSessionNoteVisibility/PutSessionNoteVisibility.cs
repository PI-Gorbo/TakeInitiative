using FastEndpoints;
using FluentValidation;
using Marten;
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
public class PutSessionNoteVisibility(IDocumentSession session)
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
            session.Events.Append(note.Id, new SessionNoteVisibilityChanged(Actor.Member(member.MemberId), req.Visibility));
            await session.SaveChangesAsync(ct);
            note = (await session.LoadAsync<SessionNote>(note.Id, ct))!;
        }

        await SendAsync(SessionNoteResponse.From(note), cancellation: ct);
    }
}
