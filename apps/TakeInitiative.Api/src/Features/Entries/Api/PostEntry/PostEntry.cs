using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Entries;

public record PostEntryRequest
{
    public Guid CampaignId { get; init; }
    public required string Name { get; init; }
    public required EntryKind Kind { get; init; }
    public required Visibility Visibility { get; init; }
}

public class PostEntryRequestValidator : Validator<PostEntryRequest>
{
    public PostEntryRequestValidator()
    {
        RuleFor(x => x.Name).EntryName();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Visibility).IsInEnum();
    }
}

/// <summary>The name and alias rules, shared by every entry endpoint that takes one.</summary>
public static class EntryNameRules
{
    /// <summary>A name (or alias) is trimmed and 1 to <see cref="Entry.NameMaxLength"/> characters long.</summary>
    public static IRuleBuilderOptions<T, string> EntryName<T>(this IRuleBuilder<T, string> rule, string what = "An entry's name")
        => rule
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage($"{what} cannot be empty.")
            .Must(name => (name ?? "").Trim().Length <= Entry.NameMaxLength)
            .WithMessage($"{what} can be at most {Entry.NameMaxLength} characters.");

    /// <summary>
    /// Aliases as stored: trimmed, de-duplicated case-insensitively (the first spelling
    /// wins), and without any that equal the name.
    /// </summary>
    public static List<string> NormalizeAliases(IEnumerable<string> aliases, string name)
        => aliases
            .Select(a => a.Trim())
            .Where(a => !string.Equals(a, name, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(a => a, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Whether <paramref name="name"/> is, ignoring case, the entry's name or one of its aliases.</summary>
    public static bool IsCalled(Entry entry, string name)
        => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase)
            || entry.Aliases.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Any member creates an entry, as its creator, with edit access <c>Anyone</c>. A name that
/// is already the name or an alias of an entry the caller can see is a 409 carrying that
/// entry's id in <c>errors.existingEntryId</c>. Entries the caller cannot see are not
/// checked, so the 409 leaks nothing; duplicates among them are fixed by merge (15g).
/// </summary>
public class PostEntry(IDocumentSession session, IHubContext<CampaignHub> hub) : Endpoint<PostEntryRequest, EntryResponse>
{
    public const string ExistingEntryIdKey = "existingEntryId";

    public override void Configure()
    {
        Post("/api/campaigns/{CampaignId}/entries");
    }

    public override async Task HandleAsync(PostEntryRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var name = req.Name.Trim();

        var visible = await session.Query<Entry>()
            .Where(e => e.CampaignId == req.CampaignId)
            .Where(EntryVisibility.VisibleTo(member))
            .ToListAsync(ct);
        var existing = visible.FirstOrDefault(e => EntryNameRules.IsCalled(e, name));
        if (existing is not null)
        {
            AddError(new ValidationFailure(nameof(PostEntryRequest.Name), $"There is already an entry called \"{existing.Name}\"."));
            AddError(new ValidationFailure(ExistingEntryIdKey, existing.Id.ToString()));
            ThrowIfAnyErrors(StatusCodes.Status409Conflict);
        }

        var entryId = Guid.NewGuid();
        session.Events.StartStream<Entry>(entryId, new EntryCreated(
            Actor: Actor.Member(member.MemberId),
            CampaignId: req.CampaignId,
            CreatorMemberId: member.MemberId,
            Name: name,
            Kind: req.Kind,
            Visibility: req.Visibility));
        await session.SaveChangesAsync(ct);

        var entry = (await session.LoadAsync<Entry>(entryId, ct))!;
        await hub.NotifyEntryUpserted(entry);
        await SendAsync(EntryResponse.From(entry), cancellation: ct);
    }
}
