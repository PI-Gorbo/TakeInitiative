using FastEndpoints;
using FluentValidation;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Images;

public record GetEntryImagesRequest
{
    public Guid CampaignId { get; init; }
    public Guid EntryId { get; init; }
    /// <summary>Only notes posted before this (a <c>postedAt</c> from the previous page). Omit it for the newest page.</summary>
    public DateTimeOffset? Before { get; init; }
    /// <summary>How many image notes to return: 30 by default, at most 60.</summary>
    public int? Take { get; init; }
}

public class GetEntryImagesRequestValidator : Validator<GetEntryImagesRequest>
{
    public GetEntryImagesRequestValidator()
    {
        RuleFor(x => x.Take).InclusiveBetween(1, GalleryResponse.MaxTake);
    }
}

/// <summary>
/// An entry's gallery (16d): the image notes the caller can see whose caption mentions the
/// entry, merged ids included (<see cref="Entry.MentionIds"/>), from
/// <see cref="MentionIndex.NotesMentioning"/> with <c>imagesOnly</c>. A mention in an article
/// block does not count: promote copies text only. An entry the caller cannot see is a 404.
/// </summary>
public class GetEntryImages(IDocumentSession session) : Endpoint<GetEntryImagesRequest, GalleryResponse>
{
    public override void Configure()
    {
        Get("/api/campaigns/{CampaignId}/entries/{EntryId}/images");
    }

    public override async Task HandleAsync(GetEntryImagesRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var entry = await this.RequireVisibleEntry(session, req.CampaignId, req.EntryId, member, ct);

        var ids = entry.MentionIds();
        var page = await MentionIndex.NotesMentioning(
            session, req.CampaignId, ids, member, req.Before,
            req.Take ?? GalleryResponse.DefaultTake, ct, imagesOnly: true);
        var all = MentionIndex.NotesMentioningQuery(session, req.CampaignId, ids, member, imagesOnly: true);

        await SendAsync(await GalleryResponse.For(session, page.Notes, page.HasOlder, all, ct), cancellation: ct);
    }
}
