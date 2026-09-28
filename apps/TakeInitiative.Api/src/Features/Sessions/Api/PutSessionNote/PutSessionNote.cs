using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;
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
    /// <summary>
    /// The note's whole, ordered image list (step 16b), at most 10. Leave it out to keep the
    /// images. New ids are attached, and images left out are deleted for real.
    /// </summary>
    public Guid[]? ImageIds { get; init; }
    /// <summary>
    /// The suggestion this edit accepts (step 23c), if any. The edit must then be exactly the
    /// suggested span linked (<see cref="SuggestionEdit.Check"/>), with the recap flag and images
    /// unchanged, and <see cref="NewEntries"/> is empty or the one entry it links. The event's
    /// Actor records the model, and so does a created entry's.
    /// </summary>
    public SuggestionRequest? Suggestion { get; init; }
}

public class PutSessionNoteRequestValidator : Validator<PutSessionNoteRequest>
{
    public PutSessionNoteRequestValidator()
    {
        // With imageIds left out the note keeps its images, so the handler checks the text.
        RuleFor(x => x.Text).SessionNoteText(x => x.ImageIds is null || x.ImageIds.Length > 0);
        RuleFor(x => x.NewEntries).NewEntriesList();
        RuleFor(x => x.ImageIds).ImageIdsList();
        RuleFor(x => x.Suggestion!).SetValidator(new SuggestionRequestValidator()).When(x => x.Suggestion is not null);
    }
}

/// <summary>
/// The author edits their note. An unchanged note appends nothing. Last write wins: only
/// the author can edit, so a conflict is the same person on two devices. <c>newEntries</c>
/// are created in the same transaction, with the note's current visibility. Only the author
/// changes the images: removed ones are marked deleted in the same transaction, and their
/// blobs deleted after it (the sweeper retries a failure).
/// </summary>
public class PutSessionNote(
    IDocumentSession session,
    IHubContext<CampaignHub> hub,
    ImageSweeper sweeper,
    [FromKeyedServices(SessionGap.ClockKey)] TimeProvider clock,
    ILogger<PutSessionNote> logger) : Endpoint<PutSessionNoteRequest, SessionNoteResponse>
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
        var actor = req.Suggestion is { } suggestion
            ? await this.SuggestionActor(session, req, note, member, text, suggestion, ct)
            : Actor.Member(member.MemberId);
        IReadOnlyList<Guid> newEntryIds;
        ImageAttachments.Staged? images;
        bool edited;
        await using (var write = await NoteWrite.Begin(session, ct))
        {
            images = req.ImageIds is null
                ? null
                : await this.Stage(write.Session, req.CampaignId, member, note.Id, note.Images, req.ImageIds, clock.GetUtcNow(), ct);
            if (text.Length == 0 && (images?.Images ?? note.Images).Length == 0)
            {
                ThrowError(new ValidationFailure("text", SessionNoteTextRule.NeedsTextMessage), StatusCodes.Status400BadRequest);
            }
            var imagesChanged = images is not null && images.Differs(note.Images);
            if (imagesChanged)
            {
                await this.SaveImagesAsync(write.Session, ct);
            }

            newEntryIds = await this.AppendNewEntries(
                write.Session, req.CampaignId, member, note.Id, text, NewEntries.VisibilityFrom(note), req.NewEntries, ct, actor);
            edited = note.Text != text || note.IsRecap != req.IsRecap || imagesChanged;
            if (edited)
            {
                write.Session.Events.Append(note.Id, new SessionNoteEdited(
                    actor, text, req.IsRecap, imagesChanged ? images!.Images : null,
                    req.Suggestion is { } accepted ? new SuggestedSpan(accepted.Start, accepted.Length, accepted.EntryId) : null));
            }
            if (edited || newEntryIds.Count > 0)
            {
                await write.Session.SaveChangesAsync(ct);
            }
            await write.CommitAsync(ct);
        }
        await sweeper.PurgeRemoved(images?.Removed ?? [], logger, ct);

        if (edited)
        {
            note = (await session.LoadAsync<SessionNote>(note.Id, ct))!;
            await hub.NotifySessionNoteUpserted(note);
        }
        await hub.NotifyCreated(session, newEntryIds, ct);

        await SendAsync(SessionNoteResponse.From(note), cancellation: ct);
    }

    /// <summary>
    /// The Actor of an edit that accepts <paramref name="suggestion"/>: the author, with the model.
    /// Anything that makes it more than the one suggested link is a 400 (<c>suggestion</c>).
    /// </summary>
    private async Task<Actor> SuggestionActor(
        IDocumentSession session, PutSessionNoteRequest req, SessionNote note, Member member, string text,
        SuggestionRequest suggestion, CancellationToken ct)
    {
        void Reject(string message)
            => ThrowError(new ValidationFailure(SuggestionEdit.ErrorKey, message), StatusCodes.Status400BadRequest);

        if (req.IsRecap != note.IsRecap
            || (req.ImageIds is not null && !req.ImageIds.SequenceEqual(note.Images.Select(i => i.ImageId))))
        {
            Reject("Accepting a suggestion cannot change the recap flag or the images.");
        }
        if (SuggestionEdit.Check(note.Text, text, suggestion.Start, suggestion.Length, suggestion.EntryId) is { } why)
        {
            Reject(why);
        }
        if (req.NewEntries is { Length: > 0 } created)
        {
            if (created.Length != 1 || created[0].Id != suggestion.EntryId)
            {
                Reject("Accepting a suggestion creates at most the one entry it links.");
            }
        }
        else if (!await session.Query<Entry>().Listed(req.CampaignId, member).AnyAsync(e => e.Id == suggestion.EntryId, ct))
        {
            // Unknown, merged and hidden entries look the same, so this says nothing about them.
            Reject("There is no entry with the suggested id.");
        }

        var confidence = double.IsFinite(suggestion.Confidence) ? Math.Clamp(suggestion.Confidence, 0, 1) : 0;
        return Actor.Suggested(member.MemberId, new ModelSuggestion(suggestion.Model.Trim(), suggestion.Version.Trim(), confidence));
    }
}
