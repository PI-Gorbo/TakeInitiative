namespace TakeInitiative.Api.Features.Sessions;

/// <summary>
/// An image on a session note, as the note's events record it (step 16b). The size is the
/// display variant's, so the web lays the note out before any byte arrives. Visibility is
/// never copied here or onto the <c>Image</c> document: an image is served to exactly the
/// note's audience, read from the note at request time (<c>ImageAccess</c>).
/// </summary>
public sealed record NoteImage(Guid ImageId, int Width, int Height);
