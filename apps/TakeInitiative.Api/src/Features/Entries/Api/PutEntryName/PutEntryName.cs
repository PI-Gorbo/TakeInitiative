using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Entries;

public record PutEntryNameRequest
{
    public Guid CampaignId { get; init; }
    public Guid EntryId { get; init; }
    public required string Name { get; init; }
}

public class PutEntryNameRequestValidator : Validator<PutEntryNameRequest>
{
    public PutEntryNameRequestValidator()
    {
        RuleFor(x => x.Name).EntryName();
    }
}

/// <summary>
/// Renames an entry. Mentions are stored by id, so no note text changes (invariant 6). The
/// same name appends nothing. An alias equal to the new name is removed with it, so an
/// alias never equals the name.
/// </summary>
public class PutEntryName(IDocumentSession session, IHubContext<CampaignHub> hub) : Endpoint<PutEntryNameRequest, EntryResponse>
{
    public override void Configure()
    {
        Put("/api/campaigns/{CampaignId}/entries/{EntryId}/name");
    }

    public override async Task HandleAsync(PutEntryNameRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var entry = await this.RequireVisibleEntry(session, req.CampaignId, req.EntryId, member, ct);
        this.RequireCanEdit(entry, member);

        var name = req.Name.Trim();
        if (entry.Name != name)
        {
            var actor = Actor.Member(member.MemberId);
            session.Events.Append(entry.Id, new EntryRenamed(actor, name));
            foreach (var alias in entry.Aliases.Where(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase)))
            {
                session.Events.Append(entry.Id, new EntryAliasRemoved(actor, alias));
            }
            await session.SaveChangesAsync(ct);
            entry = (await session.LoadAsync<Entry>(entry.Id, ct))!;
            await hub.NotifyEntryUpserted(entry);
        }

        await SendAsync(EntryResponse.From(entry, member), cancellation: ct);
    }
}
