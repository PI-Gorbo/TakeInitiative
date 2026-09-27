namespace TakeInitiative.Api.Features.Search;

/// <summary>
/// Which kind of searchable unit a <see cref="SearchDoc"/> came from (17a.2). There is no case for
/// an entry's name or alias: that unit is matched by <see cref="EntryMatcher"/>, which answers in
/// <see cref="EntryMatch"/> (it carries the matched name and whether it was an alias), so no name
/// hit is ever a <see cref="SearchDoc"/>.
/// </summary>
public enum SearchDocKind
{
    /// <summary>One block of an entry's article.</summary>
    ArticleBlock,
    /// <summary>One session note, or an image note's caption.</summary>
    SessionNote,
    /// <summary>One session, by number or title.</summary>
    Session,
}

/// <summary>
/// One searchable unit with exactly one audience (glossary: Search doc, §9). It is the row
/// shape of the search query over the inline projections and is <b>never stored</b> (Notes,
/// "Why no stored search table"): every source is updated in the same transaction as its
/// event, and Postgres maintains the expression indexes in that transaction, so a search sees
/// each post, edit, hide, visibility change, merge and delete as soon as it commits.
/// <para>
/// One audience per unit is what keeps a snippet from leaking: <see cref="Headline"/> is cut
/// from that unit alone, on a row that already passed the visibility predicate, so there is no
/// fragment boundary where text from another audience could slip in.
/// </para>
/// </summary>
/// <param name="Kind">Which source the row came from.</param>
/// <param name="SourceId">The entry, note or session id.</param>
/// <param name="BlockId">The article block, for <see cref="SearchDocKind.ArticleBlock"/> only.</param>
/// <param name="Rank">
/// <c>ts_rank_cd</c> for text, or <c>word_similarity</c> for a name. Both score one row alone:
/// Postgres keeps no corpus statistics for them, so adding a row the viewer cannot see cannot
/// move a visible one.
/// </param>
/// <param name="Category">The match category (17a.8): 0 exact … 4 fuzzy, 5 article only.</param>
/// <param name="Headline">
/// The <c>ts_headline</c> output, with <see cref="Snippet.StartMarker"/> and
/// <see cref="Snippet.StopMarker"/> around the matches, or null when the unit has no snippet.
/// </param>
public record SearchDoc(SearchDocKind Kind, Guid SourceId, Guid? BlockId, double Rank, int Category, string? Headline);
