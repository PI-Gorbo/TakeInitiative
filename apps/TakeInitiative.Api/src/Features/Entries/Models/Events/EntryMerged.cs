namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// This entry was merged (glossary: Merge) into <see cref="IntoEntryId"/>, on the merged
/// entry's stream. Its document keeps <see cref="Entry.MergedIntoId"/>, so its id redirects,
/// and lists leave it out. Appended in the same save as the target's <see cref="EntryAbsorbed"/>.
/// </summary>
public sealed record EntryMerged(Actor Actor, Guid IntoEntryId) : IActorEvent;
