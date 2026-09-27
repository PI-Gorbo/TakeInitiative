using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;
using Marten;
using Marten.Exceptions;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Entries;

public record PostEntryMergeRequest
{
    public Guid CampaignId { get; init; }
    /// <summary>The entry to merge (the one that goes away).</summary>
    public Guid EntryId { get; init; }
    /// <summary>The entry it is merged into (the one that stays).</summary>
    public required Guid IntoEntryId { get; init; }
}

public class PostEntryMergeRequestValidator : Validator<PostEntryMergeRequest>
{
    public PostEntryMergeRequestValidator()
    {
        RuleFor(x => x.IntoEntryId).NotEmpty();
        RuleFor(x => x.IntoEntryId).NotEqual(x => x.EntryId).WithMessage("An entry cannot be merged into itself.");
    }
}

/// <summary>
/// Merges one entry into another (glossary: Merge, 15g.1). The caller must be able to see and
/// edit both, and neither may be merged already (a 404 when either is hidden, a 403 when
/// either cannot be edited, a 409 when either is merged).
/// <list type="bullet">
/// <item><b>Visibility guard.</b> A 409 with <c>errors.visibility</c> ("Change visibility first")
/// unless everyone who can see the target can already see the merged entry
/// (<see cref="EntryMerge.RevealsNothing"/>).</item>
/// <item><b>Claims.</b> A claimed entry merges only into a Character that is unclaimed or has the
/// same claimer (409 <c>errors.claim</c>), and the claim moves with it.</item>
/// <item><b>Aliases.</b> The merged name and aliases become the target's aliases; a 409
/// <c>errors.aliases</c> when that would pass <see cref="Entry.MaxAliases"/>.</item>
/// </list>
/// <see cref="EntryMerged"/> and <see cref="EntryAbsorbed"/> are appended in one save, checked
/// against both streams' versions, so a racing edit to either makes this a 409 to retry. No
/// text is rewritten (invariant 6): mentions of the merged id resolve to the target. Answers
/// the target as the caller sees it.
/// </summary>
public class PostEntryMerge(IDocumentSession session, IHubContext<CampaignHub> hub) : Endpoint<PostEntryMergeRequest, EntryResponse>
{
    public const string VisibilityErrorKey = "visibility";
    public const string ClaimErrorKey = "claim";
    public const string AliasesErrorKey = "aliases";
    public const string MergedErrorKey = "merged";

    public override void Configure()
    {
        Post("/api/campaigns/{CampaignId}/entries/{EntryId}/merge");
    }

    public override async Task HandleAsync(PostEntryMergeRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (campaign, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var from = await this.RequireVisibleEntry(session, req.CampaignId, req.EntryId, member, ct, followMerge: false);
        var into = await this.RequireVisibleEntry(session, req.CampaignId, req.IntoEntryId, member, ct, followMerge: false);
        this.RequireCanEdit(from, member);
        this.RequireCanEdit(into, member);

        var fromStream = await session.Events.FetchForWriting<Entry>(from.Id, ct);
        var intoStream = await session.Events.FetchForWriting<Entry>(into.Id, ct);
        // Check again at the versions the append is checked against.
        from = fromStream.Aggregate ?? from;
        into = intoStream.Aggregate ?? into;
        if (from.MergedIntoId is not null || into.MergedIntoId is not null)
        {
            Refuse(MergedErrorKey, from.MergedIntoId is not null
                ? $"\"{from.Name}\" has already been merged."
                : $"\"{into.Name}\" has already been merged into another entry.");
        }

        switch (EntryMerge.Check(from, into, campaign.Members))
        {
            case EntryMergeError.WouldReveal:
                Refuse(VisibilityErrorKey,
                    $"Some people who can see \"{into.Name}\" cannot see \"{from.Name}\". Change visibility first.");
                break;
            case EntryMergeError.ClaimNeedsCharacter:
                Refuse(ClaimErrorKey, $"\"{from.Name}\" is a player character, so it can only be merged into a Character.");
                break;
            case EntryMergeError.ClaimedByAnother:
                Refuse(ClaimErrorKey, $"\"{from.Name}\" and \"{into.Name}\" are different members' player characters.");
                break;
        }

        var aliases = EntryNameRules.NormalizeAliases([.. into.Aliases, from.Name, .. from.Aliases], into.Name);
        if (aliases.Count > Entry.MaxAliases)
        {
            Refuse(AliasesErrorKey,
                $"\"{into.Name}\" would have {aliases.Count} aliases, and at most {Entry.MaxAliases} are allowed. Remove some first.");
        }

        var actor = Actor.Member(member.MemberId);
        fromStream.AppendOne(new EntryMerged(actor, into.Id));
        intoStream.AppendOne(new EntryAbsorbed(
            Actor: actor,
            FromEntryId: from.Id,
            FromName: from.Name,
            FromAliases: from.Aliases,
            FromBlocks: from.Article.Blocks,
            FromMergedIds: from.MergedFromIds,
            HeadingBlockId: Guid.NewGuid(),
            FromClaimedByMemberId: from.ClaimedByMemberId,
            Stats: EntryMerge.AdoptedStats(from, into)));
        try
        {
            await session.SaveChangesAsync(ct);
        }
        catch (ConcurrencyException)
        {
            Refuse(MergedErrorKey, "One of these entries changed while merging. Try again.");
        }

        var after = (await session.LoadAsync<Entry>(into.Id, ct))!;
        await hub.NotifyEntryMerged(from, after);
        await SendAsync(EntryResponse.From(after, member), cancellation: ct);
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private void Refuse(string key, string message)
        => ThrowError(new ValidationFailure(key, message), StatusCodes.Status409Conflict);
}
