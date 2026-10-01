namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// A link was added to the entry (27b). The whole <see cref="EntryLink"/> is on the event, so the
/// projection appends it and history can name it without a second lookup.
/// <para>
/// There is no <c>EntryLinkChanged</c>: editing a label is a remove and an add. Two events rather
/// than three keeps the projection trivial and the history honest — the old label is not silently
/// rewritten — at the cost of two history rows for a typo fix.
/// </para>
/// </summary>
public sealed record EntryLinkAdded(Actor Actor, EntryLink Link) : IActorEvent;
