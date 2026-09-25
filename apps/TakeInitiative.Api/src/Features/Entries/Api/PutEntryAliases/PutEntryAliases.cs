using FastEndpoints;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Entries;

public record PutEntryAliasesRequest
{
    public Guid CampaignId { get; init; }
    public Guid EntryId { get; init; }
    /// <summary>The whole list. Trimmed and de-duplicated case-insensitively; an alias equal to the name is dropped.</summary>
    public required string[] Aliases { get; init; }
}

public class PutEntryAliasesRequestValidator : Validator<PutEntryAliasesRequest>
{
    public PutEntryAliasesRequestValidator()
    {
        RuleFor(x => x.Aliases).NotNull();
        RuleForEach(x => x.Aliases).EntryName("An alias");
        RuleFor(x => x.Aliases)
            .Must(aliases => aliases.Select(a => (a ?? "").Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() <= Entry.MaxAliases)
            .When(x => x.Aliases is not null)
            .WithMessage($"An entry can have at most {Entry.MaxAliases} aliases.");
    }
}

/// <summary>
/// Replaces an entry's aliases. Appends one <see cref="EntryAliasRemoved"/> or
/// <see cref="EntryAliasAdded"/> per difference (removals first), so history reads per
/// alias. Changing only an alias's case is a removal and an addition. No difference
/// appends nothing.
/// </summary>
public class PutEntryAliases(IDocumentSession session, IHubContext<CampaignHub> hub) : Endpoint<PutEntryAliasesRequest, EntryResponse>
{
    public override void Configure()
    {
        Put("/api/campaigns/{CampaignId}/entries/{EntryId}/aliases");
    }

    public override async Task HandleAsync(PutEntryAliasesRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var entry = await this.RequireVisibleEntry(session, req.CampaignId, req.EntryId, member, ct);
        this.RequireCanEdit(entry, member);

        var wanted = EntryNameRules.NormalizeAliases(req.Aliases, entry.Name);
        var removed = entry.Aliases.Where(a => !wanted.Contains(a)).ToList();
        var added = wanted.Where(a => !entry.Aliases.Contains(a)).ToList();

        if (removed.Count > 0 || added.Count > 0)
        {
            var actor = Actor.Member(member.MemberId);
            foreach (var alias in removed)
            {
                session.Events.Append(entry.Id, new EntryAliasRemoved(actor, alias));
            }
            foreach (var alias in added)
            {
                session.Events.Append(entry.Id, new EntryAliasAdded(actor, alias));
            }
            await session.SaveChangesAsync(ct);
            entry = (await session.LoadAsync<Entry>(entry.Id, ct))!;
            await hub.NotifyEntryUpserted(entry);
        }

        await SendAsync(EntryResponse.From(entry), cancellation: ct);
    }
}
