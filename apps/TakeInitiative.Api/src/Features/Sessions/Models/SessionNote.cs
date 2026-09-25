using System.Linq.Expressions;
using Marten.Events;

namespace TakeInitiative.Api.Features.Sessions;

/// <summary>
/// Inline projection of a SessionNote stream (stream id = note id). Hidden state is
/// three flat fields so the visibility filter (<see cref="SessionNoteVisibility"/>)
/// stays a simple LINQ expression. <see cref="SessionNoteDeleted"/> deletes the document.
/// <see cref="MentionedEntryIds"/> is derived from the text on every post and edit, so the
/// events carry nothing new and replaying the stream rebuilds it (invariant 6).
/// <see cref="Images"/> and the flat <see cref="HasImages"/> (step 16b) come from the events,
/// so the Text and Images filters stay a simple LINQ expression.
/// </summary>
public record SessionNote
{
    public Guid Id { get; init; }
    public Guid CampaignId { get; init; }
    public Guid SessionId { get; init; }
    public Guid AuthorMemberId { get; init; }
    public string Text { get; init; } = "";
    public Visibility Visibility { get; init; }
    public bool IsRecap { get; init; }
    public DateTimeOffset PostedAt { get; init; }
    public bool AddedLater { get; init; }
    public DateTimeOffset? EditedAt { get; init; }
    public bool IsHidden { get; init; }
    public Guid? HiddenByMemberId { get; init; }
    public DateTimeOffset? HiddenAt { get; init; }
    /// <summary>
    /// The entries the text mentions (<see cref="MentionParser.EntryIds"/>): distinct, in order
    /// of first mention. Unknown ids and ids from other campaigns are kept and never match,
    /// because <see cref="MentionIndex"/> joins them to the campaign's visible entries.
    /// </summary>
    public Guid[] MentionedEntryIds { get; init; } = [];
    /// <summary>The note's images, in order. The text is their caption and may be empty.</summary>
    public NoteImage[] Images { get; init; } = [];
    /// <summary>
    /// Whether <see cref="Images"/> is non-empty, flat so the filters query one field. A note
    /// projected before step 16 has no such field in its JSON: <see cref="WithoutImages"/>
    /// treats a missing value as false.
    /// </summary>
    public bool HasImages { get; init; }

    public const int TextMaxLength = 10_000;
    public const int MaxImages = 10;

    /// <summary>
    /// A note with no images (the Text filter). A document projected before step 16 has no
    /// <c>HasImages</c> in its JSON, which SQL reads as null, so <c>!HasImages</c> alone would
    /// drop it: the missing <c>Images</c> key lets it through.
    /// </summary>
    public static Expression<Func<SessionNote, bool>> WithoutImages => n => !n.HasImages || n.Images == null;

    /// <summary>A note with at least one image (the Images filter).</summary>
    public static Expression<Func<SessionNote, bool>> WithImages => n => n.HasImages == true;

    /// <summary>
    /// The seam for loose ends (step 19): an image note whose caption mentions no entry, so
    /// nothing in the wiki leads to its pictures. 16c's "Tag what's in this?" nudges the author.
    /// </summary>
    public static Expression<Func<SessionNote, bool>> UntaggedImageNote
        => n => n.HasImages == true && !n.MentionedEntryIds.Any();

    public static SessionNote Create(IEvent<SessionNotePosted> @event)
    {
        var e = @event.Data;
        return new SessionNote
        {
            Id = @event.StreamId,
            CampaignId = e.CampaignId,
            SessionId = e.SessionId,
            AuthorMemberId = e.AuthorMemberId,
            Text = e.Text,
            MentionedEntryIds = MentionParser.EntryIds(e.Text),
            Images = e.Images ?? [],
            HasImages = e.Images is { Length: > 0 },
            Visibility = e.Visibility,
            IsRecap = e.IsRecap,
            PostedAt = ToMicroseconds(@event.Timestamp),
            AddedLater = e.AddedLater,
        };
    }

    public SessionNote Apply(IEvent<SessionNoteEdited> @event)
    {
        var images = @event.Data.Images ?? Images;
        return this with
        {
            Text = @event.Data.Text,
            MentionedEntryIds = MentionParser.EntryIds(@event.Data.Text),
            IsRecap = @event.Data.IsRecap,
            Images = images,
            HasImages = images.Length > 0,
            EditedAt = @event.Timestamp,
        };
    }

    public SessionNote Apply(SessionNoteVisibilityChanged e) => this with { Visibility = e.Visibility };

    public SessionNote Apply(IEvent<SessionNoteHidden> @event)
        => this with { IsHidden = true, HiddenByMemberId = @event.Data.Actor.MemberId, HiddenAt = @event.Timestamp };

    public SessionNote Apply(SessionNoteUnhidden _)
        => this with { IsHidden = false, HiddenByMemberId = null, HiddenAt = null };

    public bool ShouldDelete(SessionNoteDeleted _) => true;

    /// <summary>
    /// <see cref="PostedAt"/> is kept at Postgres's precision (microseconds). Queries compare
    /// and project it as a <c>timestamptz</c>, so a finer value (.NET ticks are 100 ns on
    /// Linux) would differ from itself between the document and SQL: a timeline cursor could
    /// skip a note, and a mention count's <c>lastMentionedAt</c> would not equal its note's
    /// <c>postedAt</c>.
    /// </summary>
    private static DateTimeOffset ToMicroseconds(DateTimeOffset value)
        => new(value.Ticks - value.Ticks % 10, value.Offset);
}
