using Marten.Events;

namespace TakeInitiative.Api.Features.Sessions;

/// <summary>
/// Inline projection of a SessionNote stream (stream id = note id). Hidden state is
/// three flat fields so the visibility filter (<see cref="SessionNoteVisibility"/>)
/// stays a simple LINQ expression. <see cref="SessionNoteDeleted"/> deletes the document.
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

    public const int TextMaxLength = 10_000;

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
            Visibility = e.Visibility,
            IsRecap = e.IsRecap,
            PostedAt = @event.Timestamp,
            AddedLater = e.AddedLater,
        };
    }

    public SessionNote Apply(IEvent<SessionNoteEdited> @event)
        => this with { Text = @event.Data.Text, IsRecap = @event.Data.IsRecap, EditedAt = @event.Timestamp };

    public SessionNote Apply(SessionNoteVisibilityChanged e) => this with { Visibility = e.Visibility };

    public SessionNote Apply(IEvent<SessionNoteHidden> @event)
        => this with { IsHidden = true, HiddenByMemberId = @event.Data.Actor.MemberId, HiddenAt = @event.Timestamp };

    public SessionNote Apply(SessionNoteUnhidden _)
        => this with { IsHidden = false, HiddenByMemberId = null, HiddenAt = null };

    public bool ShouldDelete(SessionNoteDeleted _) => true;
}
