using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Entries;

public record DeleteEntryLinkRequest
{
    public Guid CampaignId { get; init; }
    public Guid EntryId { get; init; }
    /// <summary>The link's <see cref="EntryLink.Id"/>, from <c>GET entry</c>.</summary>
    public Guid LinkId { get; init; }
}

/// <summary>
/// Removes a link from an entry (27c).
/// <list type="bullet">
/// <item>Who may remove is who may add: <see cref="EntryLinks.CanWrite"/> (403 otherwise) — the
/// entry's edit access plus the DMs, plus the member who plays a claimed Character (27d). Not only
/// whoever added it — a DM has to be able to take a player's link off an entry.</item>
/// <item>A link id that is not on the entry is a 404. It is not treated as a no-op success: the
/// member asked to remove something and nothing of that id was there, which is worth knowing.</item>
/// <item>A link the caller may not <i>read</i> cannot be removed either, and gets the same 404 — on
/// an unclaimed Character a player cannot learn a link's id in the first place, and guessing one
/// must not confirm it (invariant 5).</item>
/// </list>
/// Editing a label is a remove and an add, so a stale knowledge-base link removes exactly like a
/// live one: the row being gone has nothing to do with the member's right to their own link. The
/// response is the caller's view of the entry, and <c>entryLinksChanged</c> goes only to the members
/// who may read the links.
/// </summary>
public class DeleteEntryLink(IDocumentSession session, IHubContext<CampaignHub> hub, ReferenceCatalog reference, EntryLinkResolver links)
    : Endpoint<DeleteEntryLinkRequest, EntryResponse>
{
    public override void Configure()
    {
        Delete("/api/campaigns/{CampaignId}/entries/{EntryId}/links/{LinkId}");
        // See PostEntryLink for why there is no ProducesProblemFE(403).
        Description(b => b.ProducesProblemFE(StatusCodes.Status404NotFound));
    }

    public override async Task HandleAsync(DeleteEntryLinkRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (campaign, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var entry = await this.RequireVisibleEntry(session, req.CampaignId, req.EntryId, member, ct);
        this.RequireCanWriteLinks(entry, member);

        // EntryLinks.For, not entry.Links: the read rule decides what the caller can name, so a 404
        // for a link they may not read is the same 404 as for one that does not exist.
        if (EntryLinks.For(entry, member).All(l => l.Id != req.LinkId))
        {
            ThrowError("There is no link with the given id on this entry.", StatusCodes.Status404NotFound);
        }

        session.Events.Append(entry.Id, new EntryLinkRemoved(Actor.Member(member.MemberId), req.LinkId));
        await session.SaveChangesAsync(ct);
        var before = entry;
        entry = (await session.LoadAsync<Entry>(entry.Id, ct))!;
        await hub.NotifyEntryLinksChanged(campaign.Members, before, entry);

        await SendAsync(await EntryResponse.From(entry, member, reference, links, ct), cancellation: ct);
    }
}
