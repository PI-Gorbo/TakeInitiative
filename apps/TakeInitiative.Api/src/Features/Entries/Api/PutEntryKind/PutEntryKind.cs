using FastEndpoints;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Entries;

public record PutEntryKindRequest
{
    public Guid CampaignId { get; init; }
    public Guid EntryId { get; init; }
    public required EntryKind Kind { get; init; }
}

public class PutEntryKindRequestValidator : Validator<PutEntryKindRequest>
{
    public PutEntryKindRequestValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
    }
}

/// <summary>Changes an entry's kind. The same kind appends nothing.</summary>
public class PutEntryKind(IDocumentSession session, IHubContext<CampaignHub> hub) : Endpoint<PutEntryKindRequest, EntryResponse>
{
    public override void Configure()
    {
        Put("/api/campaigns/{CampaignId}/entries/{EntryId}/kind");
    }

    public override async Task HandleAsync(PutEntryKindRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var entry = await this.RequireVisibleEntry(session, req.CampaignId, req.EntryId, member, ct);
        this.RequireCanEdit(entry, member);

        if (entry.Kind != req.Kind)
        {
            session.Events.Append(entry.Id, new EntryKindChanged(Actor.Member(member.MemberId), req.Kind));
            await session.SaveChangesAsync(ct);
            entry = (await session.LoadAsync<Entry>(entry.Id, ct))!;
            await hub.NotifyEntryUpserted(entry);
        }

        await SendAsync(EntryResponse.From(entry), cancellation: ct);
    }
}
