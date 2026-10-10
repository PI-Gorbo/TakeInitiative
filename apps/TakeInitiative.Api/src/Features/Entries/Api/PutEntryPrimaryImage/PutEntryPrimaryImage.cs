using FastEndpoints;
using FluentValidation.Results;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Entries;

public record PutEntryPrimaryImageRequest
{
    public Guid CampaignId { get; init; }
    public Guid EntryId { get; init; }
    /// <summary>An image from the entry's gallery that everyone can see. Null removes the primary image.</summary>
    public Guid? ImageId { get; init; }
}

/// <summary>
/// Sets or removes an entry's primary image (SAM-12), the one picture that stands for it on its
/// page, in the wiki list, in search hits and on a combat row.
/// <list type="bullet">
/// <item>Whoever may edit the entry sets it (<see cref="EntryPermissions.CanEdit"/>, 403 otherwise).</item>
/// <item>The image must be one <see cref="EntryPrimaryImages.CanBePrimary"/> allows: live, of this
/// campaign, on a note everyone can see, and in the entry's gallery. Everything else is a 400
/// under <c>errors.imageId</c> with one message, so another member's image id tells the caller
/// nothing.</item>
/// </list>
/// The same image appends nothing. The response is the caller's view of the entry, and
/// <c>entryUpserted</c> goes to the entry's audience: the id is campaign-wide safe, so it needs
/// no per-viewer push of its own.
/// </summary>
public class PutEntryPrimaryImage(IDocumentSession session, IHubContext<CampaignHub> hub)
    : Endpoint<PutEntryPrimaryImageRequest, EntryResponse>
{
    public override void Configure()
    {
        Put("/api/campaigns/{CampaignId}/entries/{EntryId}/primary-image");
    }

    public override async Task HandleAsync(PutEntryPrimaryImageRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var entry = await this.RequireVisibleEntry(session, req.CampaignId, req.EntryId, member, ct);
        this.RequireCanEdit(entry, member);

        if (req.ImageId is { } imageId && !await EntryPrimaryImages.CanBePrimary(session, entry, imageId, ct))
        {
            ThrowError(
                new ValidationFailure(EntryPrimaryImages.ErrorKey, EntryPrimaryImages.UnavailableMessage),
                StatusCodes.Status400BadRequest);
        }

        if (req.ImageId != entry.PrimaryImageId)
        {
            var actor = Actor.Member(member.MemberId);
            session.Events.Append(entry.Id, req.ImageId is { } id
                ? new EntryPrimaryImageSet(actor, id)
                : (object)new EntryPrimaryImageCleared(actor));
            await session.SaveChangesAsync(ct);
            entry = (await session.LoadAsync<Entry>(entry.Id, ct))!;
            await hub.NotifyEntryUpserted(entry);
        }

        await SendAsync(await EntryResponse.From(entry, member, Resolve<ReferenceCatalog>(), Resolve<EntryLinkResolver>(), ct), cancellation: ct);
    }
}
