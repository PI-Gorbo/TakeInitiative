namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// Starts an Entry stream (stream id = entry id). <see cref="CreatorMemberId"/> is stored
/// rather than read from <see cref="Actor"/>, so the creator stays a member when Actor gains
/// a model case (design §11a). <see cref="CreatedFromNoteId"/> is provenance only: the note
/// the entry was created from (step 15b), not the §11 <c>Source</c>.
/// </summary>
public sealed record EntryCreated(
    Actor Actor,
    Guid CampaignId,
    Guid CreatorMemberId,
    string Name,
    EntryKind Kind,
    Visibility Visibility,
    Guid? CreatedFromNoteId = null) : IActorEvent;
