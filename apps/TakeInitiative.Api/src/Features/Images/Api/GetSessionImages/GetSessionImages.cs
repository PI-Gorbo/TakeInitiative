using FastEndpoints;
using FluentValidation;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Images;

public record GetSessionImagesRequest
{
    public Guid CampaignId { get; init; }
    public Guid SessionId { get; init; }
    /// <summary>Only notes posted before this (a <c>postedAt</c> from the previous page). Omit it for the newest page.</summary>
    public DateTimeOffset? Before { get; init; }
    /// <summary>How many image notes to return: 30 by default, at most 60.</summary>
    public int? Take { get; init; }
}

public class GetSessionImagesRequestValidator : Validator<GetSessionImagesRequest>
{
    public GetSessionImagesRequestValidator()
    {
        RuleFor(x => x.Take).InclusiveBetween(1, GalleryResponse.MaxTake);
    }
}

/// <summary>
/// A session's gallery (16d): the session's image notes the caller can see, read with the
/// stream's own rule (<see cref="SessionNoteVisibility.VisibleTo"/>), so a 🔒 DM or hidden
/// note is absent for a player (invariant 5). A session of another campaign is a 404.
/// </summary>
public class GetSessionImages(IDocumentSession session) : Endpoint<GetSessionImagesRequest, GalleryResponse>
{
    public override void Configure()
    {
        Get("/api/campaigns/{CampaignId}/sessions/{SessionId}/images");
    }

    public override async Task HandleAsync(GetSessionImagesRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var found = await this.RequireSession(session, req.CampaignId, req.SessionId, ct);
        var take = req.Take ?? GalleryResponse.DefaultTake;

        var all = session.Query<SessionNote>()
            .Where(n => n.CampaignId == req.CampaignId && n.SessionId == found.Id)
            .Where(SessionNote.WithImages)
            .Where(SessionNoteVisibility.VisibleTo(member));
        var query = all;
        if (req.Before is { } cursor)
        {
            query = query.Where(n => n.PostedAt < cursor);
        }

        var newestFirst = await query
            .OrderByDescending(n => n.PostedAt)
            .Take(take + 1)
            .ToListAsync(ct);

        await SendAsync(await GalleryResponse.For(
            session, newestFirst.Take(take).Reverse().ToList(), newestFirst.Count > take, all, ct), cancellation: ct);
    }
}
