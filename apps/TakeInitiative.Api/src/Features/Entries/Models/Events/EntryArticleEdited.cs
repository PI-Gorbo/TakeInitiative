namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// An edit to an entry's article: the whole article after the edit, already merged with the
/// blocks the editor could not see (<see cref="ArticleMerge"/>). Storing the whole article
/// lets every version be rebuilt and shown without replaying diffs. Events never leave the
/// server, so the secret blocks in it are safe here; every read redacts per viewer.
/// </summary>
public sealed record EntryArticleEdited(Actor Actor, IReadOnlyList<ArticleBlock> Blocks) : IActorEvent;
