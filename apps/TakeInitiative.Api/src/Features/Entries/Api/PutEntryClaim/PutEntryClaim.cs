using FastEndpoints;
using FluentValidation.Results;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Entries;

public record PutEntryClaimRequest
{
    public Guid CampaignId { get; init; }
    public Guid EntryId { get; init; }
    /// <summary>The claimer: the caller's own member id to claim, any member's for a DM. Null unclaims.</summary>
    public Guid? MemberId { get; init; }
}

/// <summary>
/// Claims or unclaims a Character entry as a player character (glossary: Claim, 15g.2).
/// <list type="bullet">
/// <item>Only a <c>Character</c> entry can be claimed (409 <c>errors.kind</c>).</item>
/// <item>A member claims an unclaimed entry they can see for themselves. Claiming for someone
/// else is a 403, and an entry someone else has claimed is a 409 <c>errors.memberId</c>.</item>
/// <item>A DM assigns any Character they can see to any member who can see it (400
/// <c>errors.memberId</c> for someone who is not a member, or cannot see the entry), or
/// unclaims it.</item>
/// <item>A claimer unclaims their own. Anyone else unclaiming is a 403.</item>
/// </list>
/// A member can claim several entries (glossary: Player character). The same claim appends
/// nothing. Pushes <c>entryUpserted</c> (the summary carries the claimer) and
/// <c>entryStatsChanged</c> to the members for whom the claim showed or hid the stats.
/// </summary>
public class PutEntryClaim(IDocumentSession session, IHubContext<CampaignHub> hub) : Endpoint<PutEntryClaimRequest, EntryResponse>
{
    public const string KindErrorKey = "kind";
    public const string MemberErrorKey = "memberId";

    public override void Configure()
    {
        Put("/api/campaigns/{CampaignId}/entries/{EntryId}/claim");
    }

    public override async Task HandleAsync(PutEntryClaimRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (campaign, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var entry = await this.RequireVisibleEntry(session, req.CampaignId, req.EntryId, member, ct);
        var isDm = member.Role == Role.DM;

        if (req.MemberId is { } claimerId)
        {
            if (entry.ClaimedByMemberId == claimerId)
            {
                await SendAsync(EntryResponse.From(entry, member), cancellation: ct);
                return;
            }
            if (!isDm && claimerId != member.MemberId)
            {
                ThrowError("Only a DM can assign a player character to someone else.", StatusCodes.Status403Forbidden);
            }
            if (entry.Kind != EntryKind.Character)
            {
                ThrowError(new ValidationFailure(KindErrorKey, "Only a Character can be claimed."), StatusCodes.Status409Conflict);
            }
            if (!isDm && entry.ClaimedByMemberId is not null)
            {
                ThrowError(new ValidationFailure(MemberErrorKey, "Someone else has already claimed this character."), StatusCodes.Status409Conflict);
            }
            var claimer = campaign.MemberById(claimerId);
            if (claimer is null)
            {
                ThrowError(new ValidationFailure(MemberErrorKey, "There is no member with the given id."), StatusCodes.Status400BadRequest);
            }
            if (!EntryVisibility.CanSee(entry, claimer))
            {
                ThrowError(new ValidationFailure(MemberErrorKey, "That member cannot see this entry. Change visibility first."), StatusCodes.Status400BadRequest);
            }
            session.Events.Append(entry.Id, new EntryClaimed(Actor.Member(member.MemberId), claimerId));
        }
        else
        {
            if (entry.ClaimedByMemberId is not { } current)
            {
                await SendAsync(EntryResponse.From(entry, member), cancellation: ct);
                return;
            }
            if (!isDm && current != member.MemberId)
            {
                ThrowError("Only the claimer and the DMs can unclaim a player character.", StatusCodes.Status403Forbidden);
            }
            session.Events.Append(entry.Id, new EntryUnclaimed(Actor.Member(member.MemberId)));
        }

        await session.SaveChangesAsync(ct);
        var before = entry;
        entry = (await session.LoadAsync<Entry>(entry.Id, ct))!;
        await hub.NotifyEntryUpserted(entry);
        await hub.NotifyEntryStatsChanged(campaign.Members, before, entry);
        await SendAsync(EntryResponse.From(entry, member), cancellation: ct);
    }
}
