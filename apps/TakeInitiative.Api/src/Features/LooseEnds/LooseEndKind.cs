namespace TakeInitiative.Api.Features.LooseEnds;

/// <summary>
/// The kinds of loose end (design §5, 19b.1). Each is a rule over a source, checked on every
/// read and never stored as a flag, so a loose end clears itself when its rule stops matching.
/// </summary>
public enum LooseEndKind
{
    /// <summary>An image note whose caption mentions no entry (<see cref="SessionNote.UntaggedImageNote"/>). Listed to its author.</summary>
    UntaggedImageNote,
    /// <summary>A note with no images that mentions no entry (<see cref="SessionNote.Unlinked"/>). Listed to its author.</summary>
    UnlinkedNote,
    /// <summary>An entry of kind <see cref="EntryKind.Other"/>. Listed to the members who can edit it.</summary>
    OtherKind,
    /// <summary>
    /// A mentioned entry whose article has no visible text for the viewer
    /// (<see cref="ArticleView.IsEmptyFor"/>). Listed to the members who can edit it.
    /// </summary>
    EmptyArticle,
}
