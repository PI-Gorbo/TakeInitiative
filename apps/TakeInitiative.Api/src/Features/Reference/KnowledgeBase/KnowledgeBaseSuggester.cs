using TakeInitiative.Api.Features.Entries;

namespace TakeInitiative.Api.Features.Reference.KnowledgeBase;

/// <summary>
/// "Does this entry look like something in the knowledge base?" (28b): one query over the entry's
/// name and its aliases, answering the rows the prompt may offer, best first.
/// </summary>
/// <remarks>
/// <para>
/// <b>No model is involved, and that is the point.</b> Step 23's GLiNER runs in the browser because
/// it has to find spans inside free text. This compares <i>one name against a corpus of names</i>,
/// which is exactly what <c>tsvector</c> and <c>pg_trgm</c> already do for ⌘K — so it is a query, it
/// runs on the server, it is deterministic, and it needs no download, no worker and no
/// <c>Actor.Model</c> provenance. A link made from one of these is a plain member action: claiming
/// model provenance for a trigram match would make 23e's "revert by model version" meaningless.
/// </para>
/// <para>
/// <b>The prompt is deliberately quiet</b>, and this class is where that is decided. Four filters, in
/// the order they cost:
/// </para>
/// <list type="number">
///   <item><description>
///   <b>The kind must be compatible</b> (<see cref="CategoriesFor" />). This alone removes most false
///   prompts, and it costs nothing: for a Place, a Faction or an Event there is no statement at all.
///   </description></item>
///   <item><description>
///   <b>Rungs 0 and 1 only</b> — exact, or the entry's name is a prefix of the row's. The rest of ⌘K's
///   ladder is noise here; see <see cref="KnowledgeBaseQueries.SuggestAsync" />.
///   </description></item>
///   <item><description>
///   <b>Nothing already linked, and nothing dismissed.</b> Both are excluded by the same mechanism,
///   because to the member they are the same answer: "I have dealt with this row."
///   </description></item>
///   <item><description>
///   <b>Nothing stale.</b> A staled row is one a prune would have deleted and kept only because some
///   entry links it (26c); it is unreachable by search and by browse, so offering it here would be
///   offering a row that resolves as "no longer in your knowledge base" the instant it is linked.
///   The statement excludes it, like every other read of the table.
///   </description></item>
/// </list>
/// <para>
/// <b>Who may see the answer is not this class's business.</b> <see cref="EntrySuggestions.CanSee" />
/// is that rule, applied by the endpoint, which answers an empty list rather than a 403 when it is
/// false.
/// </para>
/// </remarks>
public class KnowledgeBaseSuggester(KnowledgeBaseQueries queries)
{
    /// <summary>
    /// The row categories an entry of <paramref name="kind" /> may be: a <c>Character</c> may be a
    /// Monster, and an <c>Item</c> may be an Item or a Spell. <c>Place</c>, <c>Faction</c> and
    /// <c>Event</c> match <b>nothing</b> — the corpus holds monsters, spells and items, and a place
    /// named after a spell is a coincidence, not a match.
    /// <para>
    /// <b><c>Other</c> may be a Spell</b>, which 28's plan leaves out. It has to be the other way
    /// round from the rest: <c>KnowledgeBaseItemRow.Summary</c> files a Spell row under
    /// <see cref="EntryKind.Other" />, because there is no Spell kind, so + Wiki's own mapping makes
    /// <c>Other</c> the kind a spell entry has. Without this clause a corpus of spells could never be
    /// suggested at all, and "Fireball ↗ Fireball" is the least ambiguous prompt in the feature.
    /// </para>
    /// </summary>
    public static IReadOnlyList<ReferenceCategory> CategoriesFor(EntryKind kind) => kind switch
    {
        EntryKind.Character => [ReferenceCategory.Monster],
        EntryKind.Item => [ReferenceCategory.Item, ReferenceCategory.Spell],
        EntryKind.Other => [ReferenceCategory.Spell],
        _ => [],
    };

    /// <summary>
    /// How a row's key is written for the statement's exclusion list: the provider folded, the id as
    /// it is. It is not <c>EntryKnowledgeBaseLinks.Key</c> — that one is the prune's index key, which
    /// keeps the provider's own spelling — and the two must not be confused.
    /// </summary>
    public static string ExcludedKey(string provider, string itemId) => $"{provider.ToLowerInvariant()}:{itemId}";

    /// <summary>
    /// The best <paramref name="take" /> rows <paramref name="entry" /> might be, best first: better
    /// rung, then shorter name, then name. Empty when its kind matches nothing, when it has no name
    /// at all, or when nothing qualifies — and empty is the normal answer, which is why there is no
    /// "no suggestions" state anywhere in the stack.
    /// </summary>
    public Task<IReadOnlyList<KnowledgeBaseItemRow>> For(
        Entry entry, int take, CancellationToken ct)
    {
        var categories = CategoriesFor(entry.Kind);
        if (categories.Count == 0 || take <= 0)
        {
            return Task.FromResult<IReadOnlyList<KnowledgeBaseItemRow>>([]);
        }

        // The name and every alias, folded for de-duplication only: the statement folds them again
        // for matching, so an alias that differs from the name by case is one query and not two.
        var names = new List<string>();
        foreach (var name in entry.Aliases.Prepend(entry.Name))
        {
            if (!string.IsNullOrWhiteSpace(name) && !names.Any(seen => string.Equals(seen, name.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                names.Add(name.Trim());
            }
        }

        // Linked and dismissed, in one list: both mean "do not ask about this row again".
        var excluded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var link in entry.Links)
        {
            if (link is { Kind: EntryLinkKind.KnowledgeBase, Provider: { } provider, ItemId: { } itemId })
            {
                excluded.Add(ExcludedKey(provider, itemId));
            }
        }
        foreach (var dismissed in entry.DismissedSuggestions)
        {
            excluded.Add(ExcludedKey(dismissed.Provider, dismissed.ItemId));
        }

        return queries.SuggestAsync(new KnowledgeBaseSuggestionQuery(names, categories, excluded, take), ct);
    }

    /// <summary>The same, for the prompt's own cap (<see cref="EntrySuggestions.MaxSuggestions" />).</summary>
    public Task<IReadOnlyList<KnowledgeBaseItemRow>> For(Entry entry, CancellationToken ct)
        => For(entry, EntrySuggestions.MaxSuggestions, ct);
}
