namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// Promote (glossary): a quote from a session note, appended at the end of the article.
/// <see cref="Actor"/> is who promoted it; the block's owner is the note's author.
/// </summary>
public sealed record EntryQuotePromoted(Actor Actor, ArticleBlock Block) : IActorEvent;
