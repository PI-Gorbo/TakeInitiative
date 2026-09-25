namespace TakeInitiative.Api.Features.Sessions;

/// <summary>
/// A session note. It carries no per-viewer fields (the web compares
/// <see cref="AuthorMemberId"/> with the campaign's current member id), so one payload
/// can be pushed to every group allowed to see it.
/// </summary>
public record SessionNoteResponse
{
    public required Guid Id { get; init; }
    public required Guid SessionId { get; init; }
    public required Guid AuthorMemberId { get; init; }
    public required string Text { get; init; }
    public required Visibility Visibility { get; init; }
    public required bool IsRecap { get; init; }
    public required DateTimeOffset PostedAt { get; init; }
    public required bool AddedLater { get; init; }
    public DateTimeOffset? EditedAt { get; init; }
    public required bool IsHidden { get; init; }
    public Guid? HiddenByMemberId { get; init; }

    public static SessionNoteResponse From(SessionNote note) => new()
    {
        Id = note.Id,
        SessionId = note.SessionId,
        AuthorMemberId = note.AuthorMemberId,
        Text = note.Text,
        Visibility = note.Visibility,
        IsRecap = note.IsRecap,
        PostedAt = note.PostedAt,
        AddedLater = note.AddedLater,
        EditedAt = note.EditedAt,
        IsHidden = note.IsHidden,
        HiddenByMemberId = note.HiddenByMemberId,
    };
}
