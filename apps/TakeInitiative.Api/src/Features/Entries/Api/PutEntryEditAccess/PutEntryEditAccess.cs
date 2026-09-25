using FastEndpoints;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Entries;

public record PutEntryEditAccessRequest
{
    public Guid CampaignId { get; init; }
    public Guid EntryId { get; init; }
    public required EditAccess EditAccess { get; init; }
}

public class PutEntryEditAccessRequestValidator : Validator<PutEntryEditAccessRequest>
{
    public PutEntryEditAccessRequestValidator()
    {
        RuleFor(x => x.EditAccess).IsInEnum();
    }
}

/// <summary>The creator or a DM changes who can edit an entry. The same value appends nothing.</summary>
public class PutEntryEditAccess(IDocumentSession session, IHubContext<CampaignHub> hub)
    : Endpoint<PutEntryEditAccessRequest, EntryResponse>
{
    public override void Configure()
    {
        Put("/api/campaigns/{CampaignId}/entries/{EntryId}/edit-access");
    }

    public override async Task HandleAsync(PutEntryEditAccessRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var entry = await this.RequireVisibleEntry(session, req.CampaignId, req.EntryId, member, ct);
        this.RequireCreatorOrDm(entry, member);

        if (entry.EditAccess != req.EditAccess)
        {
            session.Events.Append(entry.Id, new EntryEditAccessChanged(Actor.Member(member.MemberId), req.EditAccess));
            await session.SaveChangesAsync(ct);
            entry = (await session.LoadAsync<Entry>(entry.Id, ct))!;
            await hub.NotifyEntryUpserted(entry);
        }

        await SendAsync(EntryResponse.From(entry, member), cancellation: ct);
    }
}
