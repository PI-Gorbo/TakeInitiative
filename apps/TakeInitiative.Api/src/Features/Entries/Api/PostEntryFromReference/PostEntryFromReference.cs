using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Entries;

public record PostEntryFromReferenceRequest
{
    public Guid CampaignId { get; init; }
    /// <summary>The reference provider's key: <c>srd52</c>.</summary>
    public required string Provider { get; init; }
    /// <summary>The item's id within its provider: <c>goblin-warrior</c>.</summary>
    public required string ItemId { get; init; }
    /// <summary>The entry's name. The item's name when null or absent ("Goblin Warrior" → "Goblin").</summary>
    public string? Name { get; init; }
    public required Visibility Visibility { get; init; }
}

public class PostEntryFromReferenceRequestValidator : Validator<PostEntryFromReferenceRequest>
{
    public PostEntryFromReferenceRequestValidator()
    {
        RuleFor(x => x.Provider).NotEmpty().WithMessage("Name a reference provider.");
        RuleFor(x => x.ItemId).NotEmpty().WithMessage("Name a reference item.");
        RuleFor(x => x.Name!).EntryName().When(x => x.Name is not null);
        RuleFor(x => x.Visibility).IsInEnum();
    }
}

/// <summary>
/// + Wiki (glossary: + Wiki, 20b.5): any member creates an entry from a reference item, as its
/// creator, with edit access <c>Anyone</c>, the item's suggested kind and its <c>Source</c>.
/// <list type="bullet">
/// <item>The item must be in a registered provider (404 <c>errors.itemId</c>). Search-only
/// providers are allowed (step 21): + Wiki needs only the summary.</item>
/// <item>The name, and the 409 for a name the caller can already see, are <see cref="PostEntry"/>'s.</item>
/// <item>When the caller could write the new entry's Stats (<see cref="EntryStats.CanWrite"/>,
/// which for an unclaimed Character means a DM), they are filled from the item in the same save.
/// A player's entry has none: + Wiki does not bypass the Stats rule because the numbers come from
/// the SRD. A DM fills them later with "Use SRD stats" (20d).</item>
/// </list>
/// The source follows the Stats read rule (<see cref="EntrySources"/>), so a player who adds an NPC
/// gets back an entry without it.
/// </summary>
public class PostEntryFromReference(IDocumentSession session, IHubContext<CampaignHub> hub, ReferenceCatalog reference)
    : Endpoint<PostEntryFromReferenceRequest, EntryResponse>
{
    public const string ItemIdKey = "itemId";

    public override void Configure()
    {
        Post("/api/campaigns/{CampaignId}/entries/from-reference");
        Description(b => b
            .ProducesProblemFE(StatusCodes.Status404NotFound)
            .ProducesProblemFE(StatusCodes.Status409Conflict));
    }

    public override async Task HandleAsync(PostEntryFromReferenceRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (campaign, member) = await this.RequireMember(session, req.CampaignId, userId, ct);

        var provider = reference.Get(req.Provider);
        var item = provider?.Find(req.ItemId);
        if (provider is null || item is null)
        {
            ThrowError(new ValidationFailure(ItemIdKey, "That isn't in the reference data."), StatusCodes.Status404NotFound);
        }

        var name = (req.Name ?? item.Name).Trim();
        if (name.Length is 0 or > Entry.NameMaxLength)
        {
            // Only an item's own name gets here; a request's name was validated already.
            ThrowError(new ValidationFailure(nameof(PostEntryFromReferenceRequest.Name), "Give the entry a name."), StatusCodes.Status400BadRequest);
        }

        var existing = await EntryNameRules.ExistingVisible(session, req.CampaignId, member, name, ct);
        if (existing is not null)
        {
            AddError(new ValidationFailure(nameof(PostEntryFromReferenceRequest.Name), $"There is already an entry called \"{existing.Name}\"."));
            AddError(new ValidationFailure(PostEntry.ExistingEntryIdKey, existing.Id.ToString()));
            ThrowIfAnyErrors(StatusCodes.Status409Conflict);
        }

        var created = new EntryCreated(
            Actor: Actor.Member(member.MemberId),
            CampaignId: req.CampaignId,
            CreatorMemberId: member.MemberId,
            Name: name,
            Kind: item.SuggestedKind,
            Visibility: req.Visibility,
            Source: new EntrySource(provider.Key, item.Id, item.Url ?? provider.Get(item.Id)?.Attribution.SourceUrl ?? ""));

        // The entry as it will be, for the Stats write rule: new, so unclaimed.
        var entryId = Guid.NewGuid();
        var prospective = new Entry
        {
            Id = entryId,
            CampaignId = created.CampaignId,
            CreatorMemberId = created.CreatorMemberId,
            Name = created.Name,
            Kind = created.Kind,
            Visibility = created.Visibility,
        };
        var stats = item.Stats is not null && EntryStats.CanWrite(prospective, member) ? item.Stats : null;

        object[] events = stats is null
            ? [created]
            : [created, new EntryStatsChanged(Actor.Member(member.MemberId), stats)];
        session.Events.StartStream<Entry>(entryId, events);
        await session.SaveChangesAsync(ct);

        var entry = (await session.LoadAsync<Entry>(entryId, ct))!;
        await hub.NotifyEntryUpserted(entry);
        if (stats is not null)
        {
            await hub.NotifyEntryStatsChanged(campaign.Members, entry with { Stats = null }, entry);
        }
        await SendAsync(EntryResponse.From(entry, member, reference), cancellation: ct);
    }
}
