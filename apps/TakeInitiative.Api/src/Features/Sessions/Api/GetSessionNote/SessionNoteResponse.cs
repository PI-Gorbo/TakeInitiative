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
    /// <summary>
    /// The note's images, in order (step 16b); <see cref="Text"/> is their caption and may be
    /// empty. Their bytes are at <c>GET images/{id}/{display|thumb}</c>, served to exactly the
    /// note's audience.
    /// </summary>
    public required NoteImageResponse[] Images { get; init; }

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
        Images = [.. note.Images.Select(NoteImageResponse.From)],
    };
}

/// <summary>An image on a note. The size is the display variant's, for laying the note out before the bytes arrive.</summary>
public record NoteImageResponse
{
    public required Guid Id { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }

    public static NoteImageResponse From(NoteImage image) => new() { Id = image.ImageId, Width = image.Width, Height = image.Height };
}
