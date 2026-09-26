namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// Another entry was merged into this one (15g.1), on the target's stream. It carries what
/// moves, as it was at the merge, so replaying this stream alone rebuilds the target:
/// <list type="bullet">
/// <item>the merged entry's name and aliases, which become aliases;</item>
/// <item>its blocks, appended after an ordinary block "Merged from …" with the id
/// <see cref="HeadingBlockId"/>, owned by the actor. Each block keeps its visibility and owner,
/// so secret blocks stay secret. The heading is added even when nothing follows it, so it says
/// nothing about blocks a viewer cannot see;</item>
/// <item>its id and the ids merged into it before (<see cref="FromMergedIds"/>), so mentions of
/// any of them resolve here in one step, even along a chain of merges;</item>
/// <item>its claimer, when it had one (the target was then unclaimed or had the same claimer);</item>
/// <item><see cref="Stats"/>: the merged entry's stats, when the target had none and adopting them
/// shows them to nobody new (see <c>PostEntryMerge</c>). Null otherwise.</item>
/// </list>
/// Events never leave the server, so the secret blocks in it are safe here.
/// </summary>
public sealed record EntryAbsorbed(
    Actor Actor,
    Guid FromEntryId,
    string FromName,
    IReadOnlyList<string> FromAliases,
    IReadOnlyList<ArticleBlock> FromBlocks,
    IReadOnlyList<Guid> FromMergedIds,
    Guid HeadingBlockId,
    Guid? FromClaimedByMemberId = null,
    Stats? Stats = null) : IActorEvent;
