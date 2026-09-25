using FastEndpoints;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Entries;

public record PutEntryVisibilityRequest
{
    public Guid CampaignId { get; init; }
    public Guid EntryId { get; init; }
    public required Visibility Visibility { get; init; }
}

public class PutEntryVisibilityRequestValidator : Validator<PutEntryVisibilityRequest>
{
    public PutEntryVisibilityRequestValidator()
    {
        RuleFor(x => x.Visibility).IsInEnum();
    }
}

/// <summary>
/// The creator or a DM changes who can see an entry. <c>DM</c> and <c>Me</c> stay relative
/// to the creator, so a DM who narrows another member's entry to <c>Me</c> loses it. The
/// same visibility appends nothing.
/// </summary>
public class PutEntryVisibility(IDocumentSession session, IHubContext<CampaignHub> hub)
    : Endpoint<PutEntryVisibilityRequest, EntryResponse>
{
    public override void Configure()
    {
        Put("/api/campaigns/{CampaignId}/entries/{EntryId}/visibility");
    }

    public override async Task HandleAsync(PutEntryVisibilityRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var entry = await this.RequireVisibleEntry(session, req.CampaignId, req.EntryId, member, ct);
        this.RequireCreatorOrDm(entry, member);

        if (entry.Visibility != req.Visibility)
        {
            session.Events.Append(entry.Id, new EntryVisibilityChanged(Actor.Member(member.MemberId), req.Visibility));
            await session.SaveChangesAsync(ct);
            var before = entry;
            entry = (await session.LoadAsync<Entry>(entry.Id, ct))!;
            await hub.NotifyEntryMoved(before, entry);
        }

        await SendAsync(EntryResponse.From(entry), cancellation: ct);
    }
}
