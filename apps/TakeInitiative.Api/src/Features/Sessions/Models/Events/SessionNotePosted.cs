namespace TakeInitiative.Api.Features.Sessions;

/// <summary>
/// Starts a SessionNote stream (stream id = note id). <see cref="AuthorMemberId"/> is
/// stored rather than read from <see cref="Actor"/>, so the author stays a member when
/// Actor gains a model case (design §11a). <see cref="AddedLater"/> is decided here,
/// once: the target session was not the current one when the note was posted.
/// </summary>
public sealed record SessionNotePosted(
    Actor Actor,
    Guid CampaignId,
    Guid SessionId,
    Guid AuthorMemberId,
    string Text,
    Visibility Visibility,
    bool IsRecap,
    bool AddedLater) : IActorEvent;
