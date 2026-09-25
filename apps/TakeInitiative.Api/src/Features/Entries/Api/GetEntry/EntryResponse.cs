namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// One entry, for its page and as every entry write's response: the fields of
/// <see cref="EntrySummaryResponse"/>, plus the article from 15e and the stats from 15g.
/// Flat rather than derived from the summary: a derived record with no fields of its own
/// generates <c>Summary &amp; Record&lt;string, never&gt;</c> in <c>schema.d.ts</c>, which
/// makes every field <c>never</c>.
/// </summary>
public record EntryResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required EntryKind Kind { get; init; }
    public required string[] Aliases { get; init; }
    public required Visibility Visibility { get; init; }
    public required EditAccess EditAccess { get; init; }
    public required Guid CreatorMemberId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }

    public static EntryResponse From(Entry entry) => new()
    {
        Id = entry.Id,
        Name = entry.Name,
        Kind = entry.Kind,
        Aliases = [.. entry.Aliases],
        Visibility = entry.Visibility,
        EditAccess = entry.EditAccess,
        CreatorMemberId = entry.CreatorMemberId,
        CreatedAt = entry.CreatedAt,
        UpdatedAt = entry.UpdatedAt,
    };
}
