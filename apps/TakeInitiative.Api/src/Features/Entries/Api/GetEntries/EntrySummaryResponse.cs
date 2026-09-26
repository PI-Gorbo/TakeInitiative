namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// An entry as the wiki lists it and as <c>entryUpserted</c> pushes it. It carries no
/// per-viewer fields: the web works out "can I edit" from <see cref="CreatorMemberId"/>,
/// <see cref="EditAccess"/> and its own role, so one payload goes to every allowed group.
/// Mention counts are per viewer, so they sit beside it in <see cref="EntryListItemResponse"/>
/// and are never pushed. From 15g it carries the claimer, so the wiki can mark player
/// characters, and the merged ids, so the web's directory resolves an old mention to this entry.
/// </summary>
public record EntrySummaryResponse
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
    /// <summary>The member whose player character this is, if any (15g).</summary>
    public Guid? ClaimedByMemberId { get; init; }
    /// <summary>Every entry merged into this one (15g): mentions of these ids mean this entry.</summary>
    public required Guid[] MergedFromIds { get; init; }

    public static EntrySummaryResponse From(Entry entry) => new()
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
        ClaimedByMemberId = entry.ClaimedByMemberId,
        MergedFromIds = entry.MergedFromIds,
    };
}
