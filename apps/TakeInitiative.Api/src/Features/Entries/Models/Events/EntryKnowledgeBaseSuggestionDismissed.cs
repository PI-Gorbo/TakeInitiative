namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// A member said "this entry is not that knowledge-base row" (28b). It is user intent — "I looked at
/// this and it is not that" — so it belongs on the entry's stream like every other decision, and it
/// shows in history (<c>EntryChangeType.SuggestionDismissed</c>) so the decision is traceable.
/// <para>
/// <b>Per candidate row, not per entry.</b> Dismissing a magic item does not silence a later, better
/// monster match, so the event names the row rather than only the entry.
/// </para>
/// <para>
/// <b>Campaign-wide, not per member.</b> One DM deciding "The Eye is not the Beholder" is a fact
/// about the entry rather than a personal preference, and re-asking the other DM the same question
/// is the nagging this event exists to stop. The trade-off — one DM can silence a suggestion for the
/// other — is deliberate, and it is in 28's "Decisions for the user".
/// </para>
/// <para>
/// <b>There is no "undismissed".</b> The way back is to add the link by hand, which is why the
/// prompt's <i>No</i> does not confirm: nothing is lost that the knowledge-base picker cannot put
/// back. A dismissal keyed on <see cref="Provider" /> and <see cref="ItemId" /> also survives a
/// rename or an alias edit, which is what you want — the answer was about the <i>thing</i>, not
/// about the spelling.
/// </para>
/// </summary>
/// <param name="Actor">Who dismissed it.</param>
/// <param name="Provider">A reference provider's key, as the provider spells it: <c>5etools</c>.</param>
/// <param name="ItemId">The row's id within that provider: <c>monster_beholder_mm</c>.</param>
public sealed record EntryKnowledgeBaseSuggestionDismissed(Actor Actor, string Provider, string ItemId) : IActorEvent;
